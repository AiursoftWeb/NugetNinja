FROM hub.aiursoft.cn/mcr.microsoft.com/dotnet/sdk:8.0

WORKDIR /config

RUN dotnet tool install --global Aiursoft.NugetNinja --add-source https://nuget.aiursoft.cn/v3/index.json
RUN dotnet tool install --global Aiursoft.NugetNinja.PrBot --add-source https://nuget.aiursoft.cn/v3/index.json

ENV PATH="/root/.dotnet/tools:${PATH}"