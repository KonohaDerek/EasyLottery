// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use std::fs;
use std::path::PathBuf;

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
fn start_obs_http_server(dist_dir: PathBuf) {
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
            let url_path = request.url().to_string();
            // Strip query string
            let clean_path = url_path.split('?').next().unwrap_or("/");
            // Decode percent-encoded characters and normalise
            let relative = clean_path.trim_start_matches('/');

            // Try to serve the static file directly
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
                        continue;
                    }
                }
            } else {
                // SPA fallback: return index.html for any route that doesn't match a file
                let index_path = dist_dir.join("index.html");
                match fs::read(&index_path) {
                    Ok(bytes) => (bytes, "text/html; charset=utf-8"),
                    Err(_) => {
                        let _ = request.respond(
                            tiny_http::Response::from_string("index.html not found")
                                .with_status_code(404),
                        );
                        continue;
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
    });
}

fn main() {
    tauri::Builder::default()
        .setup(|app| {
            // Resolve the dist directory by locating a known bundled resource file
            // The resources from "../dist/wwwroot/**/*" are copied alongside the binary
            let resource_dir = app
                .path_resolver()
                .resolve_resource("index.html")
                .and_then(|p| p.parent().map(|d| d.to_path_buf()))
                .unwrap_or_else(|| {
                    // Fallback: try the exe's directory
                    std::env::current_exe()
                        .ok()
                        .and_then(|p| p.parent().map(|d| d.to_path_buf()))
                        .unwrap_or_else(|| PathBuf::from("."))
                });

            println!("[OBS Server] Serving files from: {}", resource_dir.display());

            start_obs_http_server(resource_dir);
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![greet, read_config_yaml, write_config_yaml])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
