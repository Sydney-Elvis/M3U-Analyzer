namespace M3UAnalyzer.Domain.Imports;

/// <summary>Stable warning codes. Codes are persisted; never rename an existing value.</summary>
public static class ImportWarningCodes
{
    public const string M3uMissingUrl = "M3U_MISSING_URL";
    public const string M3uEmptyTitle = "M3U_EMPTY_TITLE";
    public const string M3uNoGroup = "M3U_NO_GROUP";
    public const string M3uTypeDefaulted = "M3U_TYPE_DEFAULTED";
    public const string XtreamInvalidItem = "XTREAM_INVALID_ITEM";
    public const string XtreamEmptyTitle = "XTREAM_EMPTY_TITLE";
    public const string XtreamMalformedDocument = "XTREAM_MALFORMED_DOCUMENT";
    public const string XtreamUnknownCategory = "XTREAM_UNKNOWN_CATEGORY";
    public const string XtreamAccountInfoUnavailable = "XTREAM_ACCOUNT_INFO_UNAVAILABLE";
    public const string XmltvProgrammesDropped = "XMLTV_PROGRAMMES_DROPPED";
    public const string XmltvChannelsDropped = "XMLTV_CHANNELS_DROPPED";
    public const string XmltvDocumentTruncated = "XMLTV_DOCUMENT_TRUNCATED";
}
