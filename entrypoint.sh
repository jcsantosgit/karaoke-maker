#!/bin/bash
set -e

MODELS_DIR="/var/www/karaoke-app/WhisperModels"
BASE_FILE="${MODELS_DIR}/ggml-base.bin"
MEDIUM_FILE="${MODELS_DIR}/ggml-medium.bin"

download_if_missing() {
  local file_path="$1"
  local url="$2"

  if [ -f "$file_path" ]; then
    return 0
  fi

  if [ -z "$url" ]; then
    echo "Erro: modelo ausente em ${file_path} e URL não informada."
    echo "Defina a URL via variáveis de ambiente WHISPER_BASE_URL e WHISPER_MEDIUM_URL."
    exit 1
  fi

  mkdir -p "$MODELS_DIR"
  echo "Baixando modelo Whisper: $file_path"
  curl -fL "$url" -o "${file_path}.tmp"
  mv "${file_path}.tmp" "$file_path"
}

download_if_missing "$BASE_FILE" "$WHISPER_BASE_URL"
download_if_missing "$MEDIUM_FILE" "$WHISPER_MEDIUM_URL"

# Inicia a aplicação ASP.NET na porta 5000
dotnet KaraokeApp.dll --urls "http://*:5000" &

# Inicia o Nginx em foreground
nginx -g "daemon off;"
