FROM hub.aiursoft.cn/mcr.microsoft.com/dotnet/sdk:8.0

RUN apt update
RUN apt install -y git unzip wget cron

WORKDIR /config

RUN dotnet tool install --global Aiursoft.NugetNinja --add-source https://nuget.aiursoft.cn/v3/index.json
RUN dotnet tool install --global Aiursoft.NugetNinja.PrBot --add-source https://nuget.aiursoft.cn/v3/index.json

RUN echo "cd /config && /root/.dotnet/tools/ninja-bot" > /start.sh

ENV PATH="/root/.dotnet/tools:${PATH}"

# Register a crontab job to run ninja-bot every day
RUN crontab -l | { cat; echo "0 5 * * * /start.sh > /config/log.txt 2>&1"; } | crontab -

# Run this job at the beginning with verbose output
ENTRYPOINT ["cron", "-f", "-L 15"]