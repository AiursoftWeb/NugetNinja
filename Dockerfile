FROM hub.aiursoft.com/aiursoft/internalimages/dotnet

RUN apt update
RUN apt install -y git unzip wget cron
RUN mkdir -p /config
RUN mkdir -p /config-merge
RUN mkdir -p /root/.local/share/NugetNinjaWorkspace/
RUN mkdir -p /root/.dotnet/tools

WORKDIR /app
COPY . .
RUN dotnet build -maxcpucount:1 --configuration Release --no-self-contained *.sln && \
    dotnet pack -maxcpucount:1 --configuration Release *.sln || echo "Some packaging failed!" 

RUN dotnet tool install --global Aiursoft.NugetNinja          --add-source /app/src/Aiursoft.NugetNinja/bin/Release/ && \
    dotnet tool install --global Aiursoft.NugetNinja.PrBot    --add-source /app/src/Aiursoft.NugetNinja.PrBot/bin/Release/ && \
    dotnet tool install --global Aiursoft.NugetNinja.MergeBot --add-source /app/src/Aiursoft.NugetNinja.MergeBot/bin/Release/
     
RUN /root/.dotnet/tools/ninja --version

RUN echo "cd /config       && /root/.dotnet/tools/ninja-bot"       > /start.sh       && chmod +x /start.sh
RUN echo "cd /config-merge && /root/.dotnet/tools/ninja-merge-bot" > /start-merge.sh && chmod +x /start-merge.sh

ENV PATH="/root/.dotnet/tools:${PATH}"

# Register a crontab job to run ninja-bot every day
RUN crontab -l | { cat; echo "0 5 * * * /start.sh       > /config/log.txt 2>&1";       } | crontab -

# Register multiple auto merge jobs, because some CI may run very slow.
RUN crontab -l | { cat; echo "0 6 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -
RUN crontab -l | { cat; echo "0 7 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -
RUN crontab -l | { cat; echo "0 8 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -

# Register a crontab job to run ninja-bot every day
RUN crontab -l | { cat; echo "0 17 * * * /start.sh       > /config/log.txt 2>&1";       } | crontab -

# Register multiple auto merge jobs, because some CI may run very slow.
RUN crontab -l | { cat; echo "0 18 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -
RUN crontab -l | { cat; echo "0 19 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -
RUN crontab -l | { cat; echo "0 20 * * * /start-merge.sh > /config-merge/log.txt 2>&1"; } | crontab -


VOLUME /config
VOLUME /config-merge
VOLUME /root/.local/share/NugetNinjaWorkspace/

# Run this job at the beginning with verbose output
ENTRYPOINT ["cron", "-f", "-L 15"]