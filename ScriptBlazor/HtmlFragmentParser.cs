using Microsoft.AspNetCore.Components.Rendering;
using System;
using System.Collections.Generic;
using System.Text;
using TokenType = ScriptBlazor.TemplateTokenizer.TokenType;

namespace ScriptBlazor
{
    public sealed class HtmlFragmentParser : IFragmentParser
    {
        public static readonly HtmlFragmentParser Instance = new();

        private HtmlFragmentParser()
        {
        }

        public void Parse(PeekableTokenSequence input, ITokenFilter tokenFilter,
            ICodeParser codeParser, FragmentRecorder output)
        {
            List<(string name, IComponentTypeInfo component)> tagNameStack = new();
            var stringBuilder = new StringBuilder(); //TODO pool?

            void ParseTag()
            {
                var filterAction = tokenFilter.FilterToken(TokenFilterEnv.Text, input);
                switch (filterAction)
                {
                case TokenFilterAction.BeginScriptBlock:
                    output.Write(codeParser.ParseCodeBlock(input, this));
                    break;
                case TokenFilterAction.BeginScriptExpression:
                    output.WriteContent(codeParser.ParseExpression(input, this));
                    break;
                default:
                    var (type, content) = input.Current;
                    if (type == TokenType.End)
                    {
                        break;
                    }
                    else if (type == TokenType.Symbols)
                    {
                        var startTagIndex = content.IndexOf('<');
                        if (startTagIndex == -1)
                        {
                            output.WriteMarkupContent(content.ToString());
                            input.EnsureMoveNext();
                        }
                        else
                        {
                            if (startTagIndex > 0)
                            {
                                input.SplitCurrent(startTagIndex);
                                output.WriteMarkupContent(input.Current.Content.ToString());
                                input.EnsureMoveNext(); //Always success.
                                content = input.Current.Content;
                            }
                            content = content[1..];
                            //Now we are at the beginning of a tag.

                            //Check whether it's a close ("</").
                            bool isCloseTag = false;
                            if (content.Length > 0 && content[0] == '/')
                            {
                                isCloseTag = true;
                                content = content[1..];
                            }

                            //Read the tag name (until a whitespace).
                            stringBuilder.Clear();
                            while (type != TokenType.End && type != TokenType.Whitespace)
                            {
                                if (type == TokenType.Symbols && content.Length > 0 && content[0] == '>')
                                {
                                    break;
                                }
                                stringBuilder.Append(content);
                                input.EnsureMoveNext(); //Always success.
                                (type, content) = input.Current;
                            }
                            var tagName = stringBuilder.ToString();

                            //Validate.
                            IComponentTypeInfo componentInfo = null;
                            if (type == TokenType.End)
                            {
                                throw new Exception("Unexpected EOS inside tag.");
                            }
                            if (isCloseTag)
                            {
                                if (tagNameStack.Count == 0)
                                {
                                    throw new Exception("Tag close not matching an open.");
                                }
                                var openTagInfo = tagNameStack[^1];
                                if (openTagInfo.component is null)
                                {
                                    tagName = tagName.ToLowerInvariant();
                                }
                                if (openTagInfo.name != tagName)
                                {
                                    throw new Exception("Tag close not matching an open.");
                                }
                                tagNameStack.RemoveAt(tagNameStack.Count - 1);
                                if (openTagInfo.component is not null)
                                {
                                    output.CloseComponent();
                                }
                                else
                                {
                                    output.CloseElement();
                                }
                            }
                            else
                            {
                                componentInfo = codeParser.ParseTagName(tagName);
                                if (componentInfo is null)
                                {
                                    tagName = tagName.ToLowerInvariant();
                                }
                                if (tagName == "script")
                                {
                                    throw new Exception("<script> tag is not supported.");
                                }
                                tagNameStack.Add((tagName, componentInfo));
                                if (componentInfo is not null)
                                {
                                    output.OpenComponent(componentInfo);
                                }
                                else
                                {
                                    output.OpenElement(tagName);
                                }
                            }

                            //Attribute list.
                            //This method handles everything inside a tag and  we should be at either '/' or '>'.
                            ParseAttributeList(isCloseTag, componentInfo);
                            content = input.Current.Content;
                            
                            if (content[0] == '/')
                            {
                                content = content[1..];
                                if (content.Length == 0 || content[0] != '>')
                                {
                                    throw new Exception("Invalid tag syntax.");
                                }
                                tagNameStack.RemoveAt(tagNameStack.Count - 1);
                            }
                            //content[0] should be '>'. Consume this character.
                            input.SplitCurrent(1);
                            input.EnsureMoveNext();
                        }
                    }
                    else
                    {
                        output.WriteMarkupContent(content.ToString());
                        input.EnsureMoveNext();
                    }
                    break;
                }
            }

            void SkipWhitespaces()
            {
                while (input.Current.Type == TokenType.Whitespace)
                {
                    input.EnsureMoveNext();
                }
            }

            void ParseAttributeList(bool isClose, IComponentTypeInfo componentType)
            {
                while (true)
                {
                    var (type, content) = input.Current;
                    switch (type)
                    {
                    case TokenType.Whitespace:
                        input.EnsureMoveNext();
                        break;
                    case TokenType.Text:
                    case TokenType.Symbols:
                        stringBuilder.Clear();
                        do
                        {
                            if (type == TokenType.Symbols)
                            {
                                //Check '/' and '>'.
                                {
                                    //Convert to uint to ignore -1.
                                    var index1 = (uint)content.IndexOf('/');
                                    var index2 = (uint)content.IndexOf('>');
                                    var index = Math.Min(index1, index2);
                                    if (index != uint.MaxValue)
                                    {
                                        //If it's the first char, stop.
                                        if (index == 0)
                                        {
                                            if (stringBuilder.Length == 0)
                                            {
                                                //We haven't got a new attribute. End here.
                                                return;
                                            }
                                            //Need to write this attribute (without value).
                                            break;
                                        }
                                        input.SplitCurrent((int)index);
                                    }
                                }
                                //Check '='.
                                {
                                    var index = content.IndexOf('=');
                                    if (index > 0)
                                    {
                                        input.SplitCurrent(index);
                                    }
                                    if (index == 0)
                                    {
                                        break;
                                    }
                                }
                            }
                            stringBuilder.Append(input.Current.Content);
                            input.EnsureMoveNext();
                            (type, content) = input.Current;
                        } while (type == TokenType.Text || type == TokenType.Symbols);
                        if (isClose && stringBuilder.Length > 0)
                        {
                            throw new Exception("Close tag cannot have attributes.");
                        }
                        var attrName = stringBuilder.ToString();

                        SkipWhitespaces();

                        (type, content) = input.Current;
                        if (type == TokenType.Symbols && content.Length > 0 && content[0] == '=')
                        {
                            //Consume '='.
                            if (content.Length > 1)
                            {
                                input.SplitCurrent(1);
                            }
                            input.EnsureMoveNext();

                            SkipWhitespaces();

                            //Check and consume '"'.
                            (type, content) = input.Current;
                            if (type != TokenType.Symbols || content.Length == 0 || content[0] != '"')
                            {
                                throw new Exception("Invalid attribute syntax.");
                            }
                            if (content.Length > 1)
                            {
                                input.SplitCurrent(1);
                            }
                            input.EnsureMoveNext();

                            var val = componentType is null && !attrName.StartsWith('@') ? ParseAttributeValue() :
                                codeParser.ParseAttributeExpression(input, this, componentType, ref attrName);
                            output.WriteAttribute(attrName, val);

                            //Check and consume '"'.
                            (type, content) = input.Current;
                            if (type != TokenType.Symbols || content.Length == 0 || content[0] != '"')
                            {
                                throw new Exception("Invalid attribute syntax.");
                            }
                            if (content.Length > 1)
                            {
                                input.SplitCurrent(1);
                            }
                            input.EnsureMoveNext();
                        }
                        else
                        {
                            output.WriteAttribute(attrName, null);
                        }

                        break;
                    default:
                        throw new Exception("Invalid tag syntax.");
                    }
                }
            }

            IParsedExpressionObject ParseAttributeValue()
            {
                var attrValueRecorder = new ExpressionRecorder();
                var sb = new StringBuilder();
                bool exitLoop = false;
                while (!exitLoop)
                {
                    switch (tokenFilter.FilterToken(TokenFilterEnv.AttributeValue, input))
                    {
                    case TokenFilterAction.BeginScriptBlock:
                        throw new Exception("Code block inside attribute value is not supported.");
                    case TokenFilterAction.BeginScriptExpression:
                        if (sb.Length > 0)
                        {
                            attrValueRecorder.Write(sb.ToString());
                            sb.Clear();
                        }
                        string nullAttrName = null;
                        attrValueRecorder.Write(codeParser.ParseAttributeExpression(input, this, null, ref nullAttrName));
                        break;
                    default:
                        if (input.Current.Type == TokenType.Symbols)
                        {
                            var endIndex = input.Current.Content.IndexOf('"');
                            if (endIndex == 0)
                            {
                                exitLoop = true;
                                break;
                            }
                            if (endIndex != -1)
                            {
                                input.SplitCurrent(endIndex);
                            }
                        }
                        sb.Append(input.Current.Content);
                        input.EnsureMoveNext();
                        break;
                    }
                }
                if (sb.Length > 0)
                {
                    attrValueRecorder.Write(sb.ToString());
                    sb.Clear();
                }
                return attrValueRecorder.ToParsedObject();
            }

            while (input.Current.Type != TokenType.End)
            {
                ParseTag();
                if (tagNameStack.Count == 0)
                {
                    //Only parse one tag.
                    break;
                }
            }

            if (tagNameStack.Count != 0)
            {
                throw new Exception("Tag is not closed.");
            }
        }
    }
}
