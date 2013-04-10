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
using EnvDTE;

namespace NamingFix
{
    static class CUtils
    {
        public static void SplitTypeName(string className, out string topClass, out String subClass, bool first = true)
        {
            int p = (first) ? className.IndexOf('.') : className.LastIndexOf('.');
            if (p >= 0)
            {
                topClass = className.Substring(0, p);
                subClass = className.Substring(p + 1);
            }
            else
            {
                topClass = className;
                subClass = "";
            }
        }

        public static CodeElement GetCodeElementAtTextPoint(TextPoint point, vsCMElement requestedKind, ProjectItem projectItem)
        {
            return GetCodeElementAtTextPoint(requestedKind, projectItem.FileCodeModel.CodeElements, point);
        }

        private static CodeElement GetCodeElementAtTextPoint(vsCMElement requestedKind, CodeElements codeElements, TextPoint point)
        {
            if (codeElements == null)
                return null;
            foreach (CodeElement element in codeElements)
            {
                if (element.StartPoint.GreaterThan(point) || element.EndPoint.LessThan(point))
                    continue;
                // The code element contains the point 
                // We enter in recursion, just in case there is an inner code element that also 
                // satisfies the conditions, for example, if we are searching a namespace or a class
                CodeElements members = GetCodeElementMembers(element);
                if (members != null)
                {
                    CodeElement codeElement = GetCodeElementAtTextPoint(requestedKind, members, point);
                    if (codeElement != null)
                    {
                        // A nested code element also satisfies the conditions
                        return codeElement;
                    }
                }
                if (element.GetStartPoint(vsCMPart.vsCMPartNavigate).AbsoluteCharOffset != point.AbsoluteCharOffset)
                    continue;
                return element.Kind == requestedKind ? element : null;
            }
            return null;
        }

        private static CodeElements GetCodeElementMembers(CodeElement objCodeElement)
        {
            // ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
            if (objCodeElement is CodeNamespace)
                return ((CodeNamespace)objCodeElement).Members;
            if (objCodeElement is CodeType)
                return ((CodeType)objCodeElement).Members;
            if (objCodeElement is CodeFunction)
                return ((CodeFunction)objCodeElement).Parameters;
            return null;
            // ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
        }
    }
}