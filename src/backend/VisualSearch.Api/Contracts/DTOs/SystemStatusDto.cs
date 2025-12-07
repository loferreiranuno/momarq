namespace VisualSearch.Api.Contracts.DTOs;

public record SystemStatusDto(
    bool ClipModelLoaded,
    bool YoloModelLoaded,
    bool DatabaseConnected,
    string ApiVersion
);
