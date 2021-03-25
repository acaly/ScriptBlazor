using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    //A recorded list of instructions on ICodeGenerator.
    public interface IParsedTemplateObject
    {
        void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence);
    }

    public interface IParsedExpressionObject
    {
        void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence);
    }
}
