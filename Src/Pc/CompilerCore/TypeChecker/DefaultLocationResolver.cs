using System.IO;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Microsoft.Pc.TypeChecker.AST;

namespace Microsoft.Pc.TypeChecker
{
    public class DefaultLocationResolver : ILocationResolver
    {
        private readonly ParseTreeProperty<FileInfo> originalFiles;

        public DefaultLocationResolver(ParseTreeProperty<FileInfo> originalFiles)
        {
            this.originalFiles = originalFiles;
        }

        public SourceLocation GetLocation(ParserRuleContext decl)
        {
            if (decl == null)
            {
                return new SourceLocation
                {
                    Line = -1,
                    Column = -1,
                    File = null
                };
            }

            return new SourceLocation
            {
                Line = decl.Start.Line,
                Column = decl.Start.Column + 1,
                File = originalFiles.Get(GetRoot(decl))
            };
        }

        public SourceLocation GetLocation(IParseTree ctx, IToken tok)
        {
            if (ctx == null || tok == null)
            {
                return new SourceLocation
                {
                    Line = -1,
                    Column = -1,
                    File = null
                };
            }

            return new SourceLocation
            {
                Line = tok.Line,
                Column = tok.Column + 1,
                File = originalFiles.Get(GetRoot(ctx))
            };
        }

        public SourceLocation GetLocation(IPAST node)
        {
            return GetLocation(node.SourceLocation);
        }

        private static IParseTree GetRoot(IParseTree node)
        {
            while (node?.Parent != null)
            {
                node = node.Parent;
            }

            return node;
        }
    }
}