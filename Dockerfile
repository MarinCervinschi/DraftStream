FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS restore
WORKDIR /src
COPY Directory.Build.props DraftStream.sln ./
COPY src/DraftStream.Domain/DraftStream.Domain.csproj src/DraftStream.Domain/
COPY src/DraftStream.Application/DraftStream.Application.csproj src/DraftStream.Application/
COPY src/DraftStream.Application.Notes/DraftStream.Application.Notes.csproj src/DraftStream.Application.Notes/
COPY src/DraftStream.Application.Tasks/DraftStream.Application.Tasks.csproj src/DraftStream.Application.Tasks/
COPY src/DraftStream.Application.Snippets/DraftStream.Application.Snippets.csproj src/DraftStream.Application.Snippets/
COPY src/DraftStream.Infrastructure/DraftStream.Infrastructure.csproj src/DraftStream.Infrastructure/
COPY src/DraftStream.Host/DraftStream.Host.csproj src/DraftStream.Host/
RUN dotnet restore

FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/DraftStream.Host -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS runtime
RUN adduser -D appuser
USER appuser
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DraftStream.Host.dll"]
