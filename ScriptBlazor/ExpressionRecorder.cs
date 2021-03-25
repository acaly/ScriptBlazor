using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor
{
    public sealed class ExpressionRecorder
    {
        private delegate void InstructionDelegate(ICodeGenerator c, int n, ref int s);

        private readonly List<InstructionDelegate> _list = new();

        private sealed class MergedObject : IParsedExpressionObject
        {
            public InstructionDelegate[] List { get; init; }

            public void WriteToOutput(ICodeGenerator writer, int nestLevel, ref int sequence)
            {
                foreach (var item in List)
                {
                    item(writer, nestLevel, ref sequence);
                }
            }
        }

        public void Write(IParsedExpressionObject obj)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => obj.WriteToOutput(g, n, ref s));
        }

        public void Write(string stringLiteral)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.WriteExpression(stringLiteral));
        }

        public void WriteRaw(string raw)
        {
            _list.Add((ICodeGenerator g, int n, ref int s) => g.WriteRaw(raw));
        }

        public IParsedExpressionObject ToParsedObject()
        {
            return new MergedObject
            {
                List = _list.ToArray(),
            };
        }
    }
}
