namespace ColumnPadStudio.Domain.Lists;

public readonly record struct LineMarkerInfo(ListMarkerKind Kind, int LeadingWhitespaceLength, string Prefix);
