using System.Collections.Generic;
using Vera.Documents.Visitors;

namespace Vera.Documents.Nodes
{
    public class ScopeThermalNode : CompoundThermalNode
    {
        public ScopeThermalNode(IEnumerable<IThermalNode> children, string align) : base(children)
        {
            Align = align;
        }

        public override void Accept(IThermalVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override string Type => "scope";

        public string Align { get; }

        public bool Bold { get; set; }
    }
}