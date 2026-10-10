# Универсальный Dockerfile для любого сервиса решения.
#   PROJECT — путь к .csproj, APP — имя сборки (dll) без расширения.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet restore "$PROJECT"
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG APP
ENV APP_DLL=${APP}.dll \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080 8081
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
