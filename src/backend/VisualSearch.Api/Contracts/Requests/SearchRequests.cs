namespace VisualSearch.Api.Contracts.Requests;

public record ImageSearchRequest(
    bool DetectObjects = true,
    int MaxResults = 10
);

public record TextSearchRequest(
    string Query,
    int MaxResults = 10
);
