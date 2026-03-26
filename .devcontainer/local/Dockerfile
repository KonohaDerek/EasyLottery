FROM mcr.microsoft.com/devcontainers/dotnet:1-8.0-bookworm

RUN rm -f /etc/apt/sources.list.d/yarn.list /etc/apt/sources.list.d/yarn.sources \
    && rm -f /etc/apt/trusted.gpg.d/yarn.gpg

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /etc/apt/keyrings \
    && curl -fsSL https://cli.github.com/packages/githubcli-archive-keyring.gpg \
        | gpg --dearmor -o /etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && chmod go+r /etc/apt/keyrings/githubcli-archive-keyring.gpg \
    && echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/githubcli-archive-keyring.gpg] https://cli.github.com/packages stable main" \
        > /etc/apt/sources.list.d/github-cli.list \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends gh nodejs \
    && rm -rf /var/lib/apt/lists/*

RUN npm install -g @fission-ai/openspec@latest @github/copilot@latest @openai/codex@latest

RUN mkdir -p /home/vscode/.local/share/NuGet /home/vscode/.nuget/packages \
    && chown -R vscode:vscode /home/vscode/.local /home/vscode/.nuget

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        build-essential \
        file \
        libayatana-appindicator3-dev \
        libgtk-3-dev \
        libjavascriptcoregtk-4.1-dev \
        libssl-dev \
        libwebkit2gtk-4.1-dev \
        libxdo-dev \
        pkg-config \
        wget \
        xz-utils \
    && rm -rf /var/lib/apt/lists/*