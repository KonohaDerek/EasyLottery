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

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![greet, read_config_yaml, write_config_yaml])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
