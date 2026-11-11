# Build and publish with the .NET 10 SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS publish
WORKDIR /source

# Copy project files first so NuGet restore can be cached between source changes.
COPY ["src/AmigoSecretoDigital.Web/AmigoSecretoDigital.Web.csproj", "src/AmigoSecretoDigital.Web/"]
COPY ["src/AmigoSecretoDigital.Application/AmigoSecretoDigital.Application.csproj", "src/AmigoSecretoDigital.Application/"]
COPY ["src/AmigoSecretoDigital.Core/AmigoSecretoDigital.Core.csproj", "src/AmigoSecretoDigital.Core/"]
RUN dotnet restore "src/AmigoSecretoDigital.Web/AmigoSecretoDigital.Web.csproj"

COPY . .
WORKDIR /source/src/AmigoSecretoDigital.Web
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AmigoSecretoDigital.Web.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Runtime image contains only ASP.NET Core and the published application.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Serilog and ASP.NET Core Data Protection need writable locations when running as app.
RUN mkdir -p /app/logs /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /app/logs /home/app/.aspnet

COPY --from=publish --chown=app:app /app/publish/ ./
USER app

ENTRYPOINT ["dotnet", "AmigoSecretoDigital.Web.dll"]
