FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências
COPY ["*.csproj", "./"]
RUN dotnet restore

# Copiar todo o código-fonte e compilar
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Imagem final
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Argumento para comando de entrada
ENTRYPOINT ["dotnet", "ToolBox.dll"]