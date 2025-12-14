#!/bin/bash
set -e

# Inicia a aplicação ASP.NET na porta 5000
dotnet KaraokeApp.dll --urls "http://*:5000" &

# Inicia o Nginx em foreground
nginx -g "daemon off;"
