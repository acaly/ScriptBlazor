using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScriptBlazor.LuaBlazor
{
    public sealed class LuaBlazorCodeGenerator : ICodeGenerator
    {
        //TODO should we allow non-string to be concatenated with a tostring() call?
        //(We will need to decide after we differentiate string attributes and non-string attributes.)
        private const bool AllowNonString = true;

        private readonly StringWriter _buildWriter = new();
        private readonly StringWriter _codeWriter = new();
        private StringWriter CurrentWriter => _isInCodeBlock ? _codeWriter : _buildWriter;

        private readonly StringBuilder _outputWriter = new();
        private bool _isInExpr = false;
        private bool _isFirstExpr = false;
        private bool _isInCodeBlock = false;

        public override string ToString()
        {
            _outputWriter.Clear();

            _outputWriter.AppendLine("return function()");
            _outputWriter.AppendLine("local self = {}");

            _outputWriter.Append(_codeWriter.GetStringBuilder());
            _outputWriter.AppendLine();

            _outputWriter.AppendLine("function self:setParameters(p)");
            _outputWriter.AppendLine("self.parameters = p");
            _outputWriter.AppendLine("end");

            _outputWriter.AppendLine("function self:build(__builder0)");
            _outputWriter.Append(_buildWriter.GetStringBuilder());
            _outputWriter.AppendLine();
            _outputWriter.AppendLine("end");

            _outputWriter.AppendLine("return self");
            _outputWriter.AppendLine("end");

            return _outputWriter.ToString();
        }

        public void BeginCodeBlock()
        {
            if (_isInCodeBlock)
            {
                throw new InvalidOperationException();
            }
            _isInCodeBlock = true;
        }

        public void EndCodeBlock()
        {
            if (!_isInCodeBlock)
            {
                throw new InvalidOperationException();
            }
            _isInCodeBlock = false;
        }

        public void WriteRaw(string rawCode)
        {
            CurrentWriter.Write(rawCode);
        }

        public void WriteExpression(int nestLevel, ref int sequence, IParsedExpressionObject expr)
        {
            if (!_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (_isFirstExpr)
            {
                _isFirstExpr = false;
            }
            else
            {
                CurrentWriter.Write(" .. ");
            }
            expr.WriteToOutput(this, nestLevel, ref sequence);
        }

        public void WriteExpression(string stringLiteral)
        {
            if (!_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (_isFirstExpr)
            {
                _isFirstExpr = false;
            }
            else
            {
                CurrentWriter.Write(" .. ");
            }
            WriteRaw($@"""{EscapeStringLiteral(stringLiteral)}""");
        }

        public void BeginExpressionList()
        {
            if (!_isInExpr)
            {
                throw new InvalidOperationException();
            }
            WriteRaw("(");
            _isFirstExpr = true;
        }

        public void EndExpressionList()
        {
            if (!_isInExpr || _isFirstExpr)
            {
                throw new InvalidOperationException();
            }
            WriteRaw(")");
        }

        public void WriteContent(int nestLevel, ref int sequence, IParsedExpressionObject expr)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.Write($@"__builder{nestLevel}.AddContent({sequence++}, ");
            _isInExpr = true;
            _isFirstExpr = true;
            expr.WriteToOutput(this, nestLevel, ref sequence);
            if (!_isInExpr || _isFirstExpr)
            {
                throw new InvalidOperationException();
            }
            _isInExpr = false;
            CurrentWriter.WriteLine(")");
        }

        private static string EscapeStringLiteral(string str)
        {
            //TODO more robust escaping
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public void WriteMarkupContent(int nestLevel, int sequence, string str)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.AddMarkupContent({sequence}, ""{EscapeStringLiteral(str)}"")");
        }

        public void OpenElement(int nestLevel, int sequence, string elementName)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.OpenElement({sequence}, ""{elementName}"")");
        }

        public void CloseElement(int nestLevel)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.CloseElement()");
        }

        public void OpenComponent(int nestLevel, ref int sequence, IComponentTypeInfo componentType)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.OpenComponent<{componentType}>({sequence++})");
        }

        public void CloseComponent(int nestLevel)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.CloseComponent()");
        }

        public void Attribute(int nestLevel, ref int sequence, string attributeName, IParsedExpressionObject value)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            if (value is null)
            {
                CurrentWriter.WriteLine($@"__builder{nestLevel}.AddAttribute({sequence++}, ""{EscapeStringLiteral(attributeName)}"")");
            }
            else
            {
                CurrentWriter.Write($@"__builder{nestLevel}.AddAttribute({sequence++}, ""{EscapeStringLiteral(attributeName)}"", ");
                _isInExpr = true;
                _isFirstExpr = true;
                value.WriteToOutput(this, nestLevel + 1, ref sequence);
                if (!_isInExpr || _isFirstExpr)
                {
                    throw new InvalidOperationException();
                }
                _isInExpr = false;
                CurrentWriter.WriteLine(")");
            }
        }

        public void OpenRegion(int nestLevel, int sequence)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.OpenRegion({sequence})");
        }

        public void CloseRegion(int nestLevel)
        {
            if (_isInExpr)
            {
                throw new InvalidOperationException();
            }
            if (nestLevel < 0)
            {
                throw new InvalidOperationException();
            }
            CurrentWriter.WriteLine($@"__builder{nestLevel}.CloseRegion()");
        }
    }
}
