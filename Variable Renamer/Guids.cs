// Guids.cs
// MUST match guids.h
using System;

namespace Variable_Renamer
{
    static class GuidList
    {
        public const string guidVariable_RenamerPkgString = "3d0ba8c5-842a-4c4c-9fb7-b2b562f18e7e";
        public const string guidVariable_RenamerCmdSetString = "508848dc-e39b-43ee-afc7-8500b661824a";

        public static readonly Guid guidVariable_RenamerCmdSet = new Guid(guidVariable_RenamerCmdSetString);
    };
}