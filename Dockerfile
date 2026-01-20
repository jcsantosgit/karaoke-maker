# ===============================
# Build Stage
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY src/KaraokeApp/KaraokeApp.csproj ./
RUN dotnet restore "KaraokeApp.csproj"

# Copy everything else and build
COPY src/KaraokeApp/ ./
COPY src/KaraokeApp/WhisperModels ./WhisperModels/
RUN dotnet publish "KaraokeApp.csproj" -c Release -o /app/publish

# ===============================
# Final Stage
# ===============================
FROM ubuntu:22.04
WORKDIR /var/www/karaoke-app

ENV DEBIAN_FRONTEND=noninteractive

# Install dependencies
RUN apt-get update && apt-get install -y \
    nginx \
    ffmpeg \
    python3 \
    python3-pip \
    python3-venv \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Install fonts for FFmpeg subtitles
RUN echo "ttf-mscorefonts-installer msttcorefonts/accepted-mscorefonts-eula select true" | debconf-set-selections && \
    apt-get update && apt-get install -y ttf-mscorefonts-installer && \
    rm -rf /var/lib/apt/lists/*

# Install .NET 7 Runtime
RUN curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb \
    -o packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y aspnetcore-runtime-7.0 \
    && rm -rf /var/lib/apt/lists/*

# Install Demucs
RUN python3 -m venv /opt/demucs-venv && \
    /opt/demucs-venv/bin/pip install --upgrade pip && \
    /opt/demucs-venv/bin/pip install demucs && \
    /opt/demucs-venv/bin/pip install torchcodec && \
    ln -s /opt/demucs-venv/bin/demucs /usr/local/bin/demucs

# Nginx config
COPY nginx.conf /etc/nginx/sites-available/karaoke.conf
RUN rm -f /etc/nginx/sites-enabled/default \
    && ln -s /etc/nginx/sites-available/karaoke.conf /etc/nginx/sites-enabled/karaoke.conf

# Copy published app from build stage
COPY --from=build /app/publish .

# Copy Whisper models from build stage
COPY --from=build /src/WhisperModels ./WhisperModels

# Copy entrypoint script
COPY entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

EXPOSE 80

CMD ["/entrypoint.sh"]
