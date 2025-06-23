# Roadmap – New Agent Tools

## 1. OS / Shell
- `copy_file`, `move_file`, `rename_file`
- `zip_directory`, `unzip_archive`
- `change_permissions` (Unix `chmod`, Windows `icacls`)
- `kill_process`, `start_background_process`

## 2. Networking
- `tcp_ping` (open-port check)
- `dns_lookup`
- `download_file`
- `upload_file` (multipart/form-data)

## 3. HTTP / Web
- `http_request` (done) ➜ extend with:
  - auth headers (Basic / Bearer)
  - timeout override
  - follow-redirect flag
- `graphql_query`
- `websocket_send_receive`

## 4. Text / Data
- `json_path_query`
- `yaml_to_json`, `json_to_yaml`
- `csv_parse`, `csv_generate`
- `base64_encode`, `base64_decode`

## 5. Math / Stats
- `average`, `median`, `std_deviation`
- `random_int`, `random_float`

## 6. Date / Time
- `parse_date`
- `add_timespan`
- `timestamp_to_iso`, `iso_to_timestamp`

## 7. Security / Crypto
- `hash_string` (MD5, SHA-256, etc.)
- `hmac_sign`
- `aes_encrypt`, `aes_decrypt`

## 8. AI / ML Helpers
- `embedding_similarity`
- `summarize_text`
- `translate_text`

## 9. Git
- `git_clone`
- `git_current_branch`
- `git_diff`

---

Each bullet can be implemented as a static method inside a `[McpServerToolType]` class under `src/MCPServer/Tools/`. Keep argument lists small and return human-readable summaries (stdout, counts, paths, etc.).