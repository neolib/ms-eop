namespace IpamFix
{
    class ValidationRecord
    {
        internal string Id;
        internal string AddressSpace;
        internal string Environment;
        internal string IpString;
        internal string Prefix;
        internal string Forest;
        internal string EopDcName;
        internal string IpamDcName;
        internal string Title;
        internal string Region;
        internal string Status;
        internal string Summary;
        internal string Comment;
    }

    enum ValidationStatus
    {
        Unknown,
        Success,
        NoMatch,            // No matching record found in Kusto
        MultipleMatches,    // A specific IP has multiple prefixes
        WrongAddressSpace,  // In wrong addressspace
        Obsolete,           // Should be removed in config
        NoMappingDcName,    // EOP datacenter name has no mapping Azure name
        EmptyTitle,
        EmptyDatacenter,
        EmptyRegion,
        MismatchedDcName,   // Azure name does not match EOP name
        InvalidTitle,       // No datacenter/forest name in title
        InvalidRegion,
        DubiousTitle,       // Title has dubious words
    }
}
