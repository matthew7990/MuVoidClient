#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod http_updater;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            http_updater::get_client_path,
            http_updater::get_client_source_path,
            http_updater::set_client_source_path,
            http_updater::get_client_info,
            http_updater::check_server_online,
            http_updater::open_client_folder,
            http_updater::open_url,
            http_updater::check_launcher_update,
            http_updater::start_launcher_update,
            http_updater::check_and_update_client,
            http_updater::launch_game,
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
