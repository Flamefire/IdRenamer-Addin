#region license
// /*
//     This file is part of Naming Fix AddIn.
// 
//     Naming Fix AddIn is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     Naming Fix AddIn is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with Naming Fix AddIn. If not, see <http://www.gnu.org/licenses/>.
//  */
#endregion

using System;
using System.Collections.Generic;

namespace NamingFix
{
    enum ENamingStyle
    {
        UpperCamelCase,
        LowerCamelCase,
        UpperCase,
        LowerCase
    }

    struct SRenameRule
    {
        public String DontChangePrefix;
        public String Prefix;
        public ENamingStyle NamingStyle;
    }

    class CRenameRuleSet
    {
        public const int Priv = 0;
        public const int Prot = 1;
        public const int Pub = 2;

        public readonly List<String> Abbreviations = new List<String>();
        public readonly List<String> PartialAbbreviations = new List<String>();
        public readonly List<String> FixedNames = new List<String>();

        public readonly bool IdStartsWithLetter;
        public readonly string RemoveNonLetterPrefixNoReport = "_";

        public SRenameRule
            Parameter,
            LokalVariable,
            LokalConst,
            Interface,
            Class,
            Enum,
            EnumMember,
            Struct,
            Event,
            Delegate;
        public readonly SRenameRule[]
            Const,
            Field,
            Property,
            Method;

        public CRenameRuleSet()
        {
            //TODO: Define your rules here or implement a config screen!
            Const = new SRenameRule[3];
            Field = new SRenameRule[3];
            Property = new SRenameRule[3];
            Method = new SRenameRule[3];
            Parameter.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalVariable.NamingStyle = ENamingStyle.LowerCamelCase;
            LokalConst.NamingStyle = ENamingStyle.LowerCamelCase;
            Const[Priv].Prefix = "_";
            Field[Priv].Prefix = "_";
            Method[Priv].Prefix = "_";
            Property[Priv].Prefix = "_";
            Const[Prot].Prefix = "_";
            Field[Prot].Prefix = "_";
            Method[Prot].Prefix = "_";
            Property[Prot].Prefix = "_";
            EnumMember.DontChangePrefix = "TR_";
            Interface.Prefix = "I";
            Class.Prefix = "C";
            Enum.Prefix = "E";
            Struct.Prefix = "S";

            IdStartsWithLetter = true;

            Abbreviations.Add("ID");
            Abbreviations.Add("BG");
            Abbreviations.Add("FG");
            Abbreviations.Add("XML");
            Abbreviations.Add("FPS");
            Abbreviations.Add("BPM");
            Abbreviations.Add("LB");
            Abbreviations.Add("RB");
            Abbreviations.Add("MB");
            Abbreviations.Add("LD");
            Abbreviations.Add("DB");
            Abbreviations.Add("AA");
            Abbreviations.Add("DX");
            Abbreviations.Add("DY");
            Abbreviations.Add("RC");
            Abbreviations.Add("USDX");
            Abbreviations.Add("LED");
            Abbreviations.Add("PID");
            Abbreviations.Add("VID");
            Abbreviations.Add("IR");
            Abbreviations.Add("RGB");
            Abbreviations.Add("BGR");
            Abbreviations.Add("IO");
            Abbreviations.Add("API");
            Abbreviations.Add("HID");
            Abbreviations.Add("PBO");
            //Audio stuff
            Abbreviations.Add("MME");
            Abbreviations.Add("ASIO");
            Abbreviations.Add("OSS");
            Abbreviations.Add("ALSA");
            Abbreviations.Add("OS");
            Abbreviations.Add("WDMKS");
            Abbreviations.Add("JACK");
            Abbreviations.Add("WASAPI");
            Abbreviations.Add("HPI");
            Abbreviations.Add("AC");

            Abbreviations.Add("PA"); //PortAudio
            Abbreviations.Add("AL"); //OpenAL
            Abbreviations.Add("GL"); //OpenGL
            Abbreviations.Add("CV"); //OpenCV

            PartialAbbreviations.Add("MP3");
            PartialAbbreviations.Add("ZValue");
            PartialAbbreviations.Add("ZFar");
            PartialAbbreviations.Add("ZNear");
            PartialAbbreviations.Add("ZSort");
            PartialAbbreviations.Add("ZOffset");
            PartialAbbreviations.Add("HAlign");
            PartialAbbreviations.Add("VAlign");
            PartialAbbreviations.Add("AForgeNet");
            PartialAbbreviations.Add("FFmpeg");
            PartialAbbreviations.Add("VSync");
            PartialAbbreviations.Add("UInt");
            PartialAbbreviations.Add("UTF8");

            FixedNames.Add("x86");
            FixedNames.Add("x64");
            FixedNames.Add("D3DTextures");
        }
    }
}