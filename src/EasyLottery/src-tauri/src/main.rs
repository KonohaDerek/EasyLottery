// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use reqwest::blocking::Client;
use std::fs;
use std::path::{Path, PathBuf};

// Learn more about Tauri commands at https://tauri.app/v1/guides/features/command
#[tauri::command]
fn greet(name: &str) -> String {
    format!("Hello, {}! You've been greeted from Rust!", name)
}

#[tauri::command]
fn read_config_yaml(app_handle: tauri::AppHandle) -> Result<String, String> {
    let path = resolve_config_yaml_path(&app_handle)?;
    if !path.exists() {
        return Ok(String::new());
    }

    fs::read_to_string(path).map_err(|error| error.to_string())
}

#[tauri::command]
fn write_config_yaml(app_handle: tauri::AppHandle, content: String) -> Result<(), String> {
    let path = resolve_config_yaml_path(&app_handle)?;
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent).map_err(|error| error.to_string())?;
    }

    fs::write(path, content).map_err(|error| error.to_string())
}

fn resolve_config_yaml_path(app_handle: &tauri::AppHandle) -> Result<PathBuf, String> {
    let config_dir = tauri::api::path::app_config_dir(&app_handle.config())
        .ok_or_else(|| String::from("Unable to resolve app config directory."))?;
    Ok(config_dir.join("easy-lottery.yaml"))
}

/// OBS HTTP server port
const OBS_SERVER_PORT: u16 = 18930;

enum ObsServerMode {
    Static { dist_dir: PathBuf },
    DevProxy { base_url: String, client: Client },
}

fn resolve_dev_server_url() -> Option<String> {
    let config: serde_json::Value = serde_json::from_str(include_str!("../tauri.conf.json")).ok()?;
    config
        .get("build")?
        .get("devPath")?
        .as_str()
        .map(|value| value.trim_end_matches('/').to_string())
}

fn find_obs_asset_root(app_handle: &tauri::AppHandle) -> PathBuf {
    let mut candidates = Vec::new();

    for relative_path in [
        "index.html",
        "wwwroot/index.html",
        "dist/wwwroot/index.html",
        "_framework/blazor.webassembly.js",
        "wwwroot/_framework/blazor.webassembly.js",
        "dist/wwwroot/_framework/blazor.webassembly.js",
    ] {
        if let Some(resolved) = app_handle.path_resolver().resolve_resource(relative_path) {
            let root = if relative_path.ends_with("index.html") {
                resolved.parent().map(Path::to_path_buf)
            } else {
                resolved
                    .parent()
                    .and_then(Path::parent)
                    .map(Path::to_path_buf)
            };

            push_unique_path(&mut candidates, root);
        }
    }

    if let Ok(exe_path) = std::env::current_exe() {
        if let Some(exe_dir) = exe_path.parent() {
            push_unique_path(&mut candidates, Some(exe_dir.to_path_buf()));
            push_unique_path(&mut candidates, Some(exe_dir.join("resources")));
            push_unique_path(&mut candidates, Some(exe_dir.join("Resources")));

            if let Some(parent_dir) = exe_dir.parent() {
                push_unique_path(&mut candidates, Some(parent_dir.join("resources")));
                push_unique_path(&mut candidates, Some(parent_dir.join("Resources")));
            }
        }
    }

    for candidate in &candidates {
        if is_obs_asset_root(candidate) {
            return candidate.clone();
        }
    }

    for candidate in &candidates {
        if let Some(found) = search_obs_asset_root(candidate, 4) {
            return found;
        }
    }

    let fallback = std::env::current_exe()
        .ok()
        .and_then(|path| path.parent().map(Path::to_path_buf))
        .unwrap_or_else(|| PathBuf::from("."));

    eprintln!(
        "[OBS Server] Unable to find bundled OBS assets. Falling back to {}",
        fallback.display()
    );

    fallback
}

fn push_unique_path(paths: &mut Vec<PathBuf>, candidate: Option<PathBuf>) {
    if let Some(candidate) = candidate {
        if !paths.iter().any(|existing| existing == &candidate) {
            paths.push(candidate);
        }
    }
}

fn is_obs_asset_root(path: &Path) -> bool {
    path.join("index.html").is_file()
        && path
            .join("_framework")
            .join("blazor.webassembly.js")
            .is_file()
}

fn search_obs_asset_root(base_dir: &Path, max_depth: usize) -> Option<PathBuf> {
    if !base_dir.is_dir() {
        return None;
    }

    if is_obs_asset_root(base_dir) {
        return Some(base_dir.to_path_buf());
    }

    if max_depth == 0 {
        return None;
    }

    let entries = fs::read_dir(base_dir).ok()?;
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }

        if let Some(found) = search_obs_asset_root(&path, max_depth - 1) {
            return Some(found);
        }
    }

    None
}

/// Guess MIME type from file extension
fn guess_mime(path: &std::path::Path) -> &'static str {
    match path.extension().and_then(|e| e.to_str()) {
        Some("html") | Some("htm") => "text/html; charset=utf-8",
        Some("css") => "text/css; charset=utf-8",
        Some("js") | Some("mjs") => "application/javascript; charset=utf-8",
        Some("json") => "application/json; charset=utf-8",
        Some("wasm") => "application/wasm",
        Some("png") => "image/png",
        Some("jpg") | Some("jpeg") => "image/jpeg",
        Some("gif") => "image/gif",
        Some("svg") => "image/svg+xml",
        Some("ico") => "image/x-icon",
        Some("woff") => "font/woff",
        Some("woff2") => "font/woff2",
        Some("ttf") => "font/ttf",
        Some("eot") => "application/vnd.ms-fontobject",
        Some("mp4") => "video/mp4",
        Some("webm") => "video/webm",
        Some("webp") => "image/webp",
        Some("xml") => "text/xml; charset=utf-8",
        Some("txt") => "text/plain; charset=utf-8",
        Some("dll") => "application/octet-stream",
        Some("dat") => "application/octet-stream",
        Some("blat") => "application/octet-stream",
        Some("pdb") => "application/octet-stream",
        _ => "application/octet-stream",
    }
}

/// Start a background HTTP static file server so OBS browser sources can access the Blazor WASM pages
fn start_obs_http_server(mode: ObsServerMode) {
    std::thread::spawn(move || {
        let addr = format!("0.0.0.0:{}", OBS_SERVER_PORT);
        let server = match tiny_http::Server::http(&addr) {
            Ok(s) => {
                println!("[OBS Server] Static file server started on http://localhost:{}", OBS_SERVER_PORT);
                s
            }
            Err(e) => {
                eprintln!("[OBS Server] Failed to bind {}: {}", addr, e);
                return;
            }
        };

        for request in server.incoming_requests() {
            match &mode {
                ObsServerMode::Static { dist_dir } => serve_static_request(request, dist_dir),
                ObsServerMode::DevProxy { base_url, client } => proxy_dev_request(request, base_url, client),
            }
        }
    });
}

fn serve_static_request(request: tiny_http::Request, dist_dir: &std::path::Path) {
    let url_path = request.url().to_string();
    let clean_path = url_path.split('?').next().unwrap_or("/");
    let relative = clean_path.trim_start_matches('/');

    let file_path = if relative.is_empty() {
        dist_dir.join("index.html")
    } else {
        dist_dir.join(relative)
    };

    let (data, mime) = if file_path.is_file() {
        match fs::read(&file_path) {
            Ok(bytes) => (bytes, guess_mime(&file_path)),
            Err(_) => {
                let _ = request.respond(
                    tiny_http::Response::from_string("500 Internal Server Error")
                        .with_status_code(500),
                );
                return;
            }
        }
    } else {
        let index_path = dist_dir.join("index.html");
        match fs::read(&index_path) {
            Ok(bytes) => (bytes, "text/html; charset=utf-8"),
            Err(_) => {
                let _ = request.respond(
                    tiny_http::Response::from_string("index.html not found")
                        .with_status_code(404),
                );
                return;
            }
        }
    };

    let response = tiny_http::Response::from_data(data)
        .with_header(
            tiny_http::Header::from_bytes(
                &b"Content-Type"[..],
                mime.as_bytes(),
            )
            .unwrap(),
        )
        .with_header(
            tiny_http::Header::from_bytes(
                &b"Access-Control-Allow-Origin"[..],
                &b"*"[..],
            )
            .unwrap(),
        );

    let _ = request.respond(response);
}

fn proxy_dev_request(request: tiny_http::Request, base_url: &str, client: &Client) {
    let target_url = format!("{}{}", base_url, request.url());
    let proxied = match client.get(&target_url).send() {
        Ok(response) => response,
        Err(error) => {
            let _ = request.respond(
                tiny_http::Response::from_string(format!("OBS dev proxy failed: {}", error))
                    .with_status_code(502),
            );
            return;
        }
    };

    let status = proxied.status().as_u16();
    let content_type = proxied
        .headers()
        .get(reqwest::header::CONTENT_TYPE)
        .and_then(|value| value.to_str().ok())
        .unwrap_or("application/octet-stream")
        .to_string();

    let body = match proxied.bytes() {
        Ok(bytes) => bytes,
        Err(error) => {
            let _ = request.respond(
                tiny_http::Response::from_string(format!("OBS dev proxy body read failed: {}", error))
                    .with_status_code(502),
            );
            return;
        }
    };

    let response = tiny_http::Response::from_data(body.to_vec())
        .with_status_code(status)
        .with_header(
            tiny_http::Header::from_bytes(
                &b"Content-Type"[..],
                content_type.as_bytes(),
            )
            .unwrap(),
        )
        .with_header(
            tiny_http::Header::from_bytes(
                &b"Access-Control-Allow-Origin"[..],
                &b"*"[..],
            )
            .unwrap(),
        );

    let _ = request.respond(response);
}

fn main() {
    tauri::Builder::default()
        .setup(|app| {
            let server_mode = if cfg!(debug_assertions) {
                if let Some(dev_server_url) = resolve_dev_server_url() {
                    println!("[OBS Server] Proxying development server from: {}", dev_server_url);
                    ObsServerMode::DevProxy {
                        base_url: dev_server_url,
                        client: Client::builder()
                            .danger_accept_invalid_certs(true)
                            .build()
                            .expect("failed to create OBS dev proxy client"),
                    }
                } else {
                    let resource_dir = find_obs_asset_root(&app.handle());

                    println!("[OBS Server] Serving fallback static files from: {}", resource_dir.display());
                    ObsServerMode::Static { dist_dir: resource_dir }
                }
            } else {
                let resource_dir = find_obs_asset_root(&app.handle());

                println!("[OBS Server] Serving bundled files from: {}", resource_dir.display());
                ObsServerMode::Static { dist_dir: resource_dir }
            };

            start_obs_http_server(server_mode);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![greet, read_config_yaml, write_config_yaml])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
