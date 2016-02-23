﻿using Microsoft.CodeAnalysis.Semantics;
using Pchp.Syntax;
using Pchp.Syntax.AST;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Semantics.Graph
{
    /// <summary>
    /// Represents edge between to graph blocks.
    /// </summary>
    public abstract class Edge : AstNode
    {
        /// <summary>
        /// Properties key as a recursion prevention when visiting edges recursively.
        /// </summary>
        protected static readonly object/*!*/RecursionLockKey = new object();

        /// <summary>
        /// Target blocks.
        /// </summary>
        public abstract IEnumerable<Block>/*!!*/Targets { get; }

        /// <summary>
        /// Gets value indicating whether the edge represents a conditional edge.
        /// </summary>
        public virtual bool IsConditional => false;

        /// <summary>
        /// Gets value indicating whether the edge represents try/catch.
        /// </summary>
        public virtual bool IsTryCatch => false;

        /// <summary>
        /// Gets value indicating whether the edge represents switch.
        /// </summary>
        public virtual bool IsSwitch => false;

        /// <summary>
        /// Condition expression of conditional edge.
        /// </summary>
        public virtual BoundExpression Condition => null;

        /// <summary>
        /// Catch blocks if try/catch edge.
        /// </summary>
        public virtual CatchBlock[] CatchBlocks => EmptyArray<CatchBlock>.Instance;

        /// <summary>
        /// Finally block of try/catch edge.
        /// </summary>
        public virtual Block FinallyBlock => null;

        /// <summary>
        /// Enumeration with single case blocks.
        /// </summary>
        public virtual CaseBlock[] CaseBlocks => EmptyArray<CaseBlock>.Instance;

        internal Edge(Block/*!*/source)
        {
            Contract.ThrowIfNull(source);
        }

        protected void Connect(Block/*!*/source)
        {
            source.NextEdge = this;
        }

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public abstract void Visit(GraphVisitor visitor);
    }

    /// <summary>
    /// Represents simple unconditional jump.
    /// </summary>
    [DebuggerDisplay("SimpleEdge")]
    public class SimpleEdge : Edge
    {
        /// <summary>
        /// Gets the target block if the simple edge.
        /// </summary>
        public Block Target { get { return _target; } }
        private readonly Block _target;

        internal SimpleEdge(Block source, Block target)
            : base(source)
        {
            _target = target;
            Connect(source);
        }

        /// <summary>
        /// Target blocks.
        /// </summary>
        public override IEnumerable<Block> Targets => new Block[] { _target };
        
        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor) => visitor.VisitCFGSimpleEdge(this);
    }

    /// <summary>
    /// Conditional edge.
    /// </summary>
    [DebuggerDisplay("ConditionalEdge")]
    public sealed class ConditionalEdge : Edge
    {
        private readonly Block _true, _false;
        private readonly BoundExpression _condition;

        /// <summary>
        /// Target true block
        /// </summary>
        public Block/*!*/TrueTarget => _true;

        /// <summary>
        /// Target false block.
        /// </summary>
        public Block/*!*/FalseTarget => _false;

        internal ConditionalEdge(Block source, Block @true, Block @false, BoundExpression cond)
            : base(source)
        {
            Debug.Assert(@true != @false);

            _true = @true;
            _false = @false;
            _condition = cond;

            Connect(source);
        }

        /// <summary>
        /// All target blocks.
        /// </summary>
        public override IEnumerable<Block> Targets => new Block[] { _true, _false };

        public override bool IsConditional
        {
            get { return true; }
        }

        public override BoundExpression Condition => _condition;

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor) => visitor.VisitCFGConditionalEdge(this);
    }

    /// <summary>
    /// Represents try/catch edge.
    /// </summary>
    [DebuggerDisplay("TryCatchEdge")]
    public sealed class TryCatchEdge : Edge
    {
        private readonly Block _body;
        private readonly CatchBlock[] _catchBlocks;
        private readonly Block _finallyBlock;

        /// <summary>
        /// Try block.
        /// </summary>
        public Block BodyBlock => _body;

        /// <summary>
        /// Whether the given class name is equal to <c>Exception</c>.
        /// </summary>
        private static bool IsExceptionClassName(DirectTypeRef tref)
        {
            return
                tref.GenericParams.Count == 0 &&
                tref.ClassName == NameUtils.SpecialNames.Exception;
        }

        internal CatchBlock HandlingCatch(QualifiedName exceptionClassName)
        {
            foreach (var block in _catchBlocks)
                if (block.TypeRef.ClassName == exceptionClassName || IsExceptionClassName(block.TypeRef))
                    return block;

            return null;
        }

        internal TryCatchEdge(Block source, Block body, CatchBlock[] catchBlocks, Block finallyBlock)
            : base(source)
        {
            _body = body;
            _catchBlocks = catchBlocks;
            _finallyBlock = finallyBlock;
            Connect(source);
        }

        /// <summary>
        /// All target blocks.
        /// </summary>
        public override IEnumerable<Block> Targets
        {
            get
            {
                var list = new List<Block>(_catchBlocks.Length + 2);

                list.Add(_body);
                list.AddRange(_catchBlocks);
                if (_finallyBlock != null)
                    list.Add(_finallyBlock);

                return list;
            }
        }

        public override bool IsTryCatch => true;

        public override CatchBlock[] CatchBlocks => _catchBlocks;

        public override Block FinallyBlock => _finallyBlock;

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor)
        {
            visitor.VisitCFGTryCatchEdge(this);
        }
    }

    /// <summary>
    /// Represents foreach edge through the enumeree invocation.
    /// </summary>
    [DebuggerDisplay("ForeachEnumeree")]
    public sealed class ForeachEnumereeEdge : SimpleEdge
    {
        /// <summary>
        /// Array to enumerate through.
        /// </summary>
        public BoundExpression Enumeree => _enumeree;
        private readonly BoundExpression _enumeree;

        internal ForeachEnumereeEdge(Block/*!*/source, Block/*!*/target, BoundExpression/*!*/enumeree)
            : base(source, target)
        {
            Contract.ThrowIfNull(enumeree);
            _enumeree = enumeree;
        }

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor)
        {
            visitor.VisitCFGForeachEnumereeEdge(this);
        }
    }

    /// <summary>
    /// Represents foreach edge from enumeree invocation through <c>MoveNext</c> to body block or end.
    /// </summary>
    [DebuggerDisplay("ForeachMoveNextEdge")]
    public sealed class ForeachMoveNextEdge : Edge
    {
        /// <summary>
        /// Content of the foreach.
        /// </summary>
        public Block BodyBlock => _body;

        /// <summary>
        /// Block after the foreach.
        /// </summary>
        public Block EndBlock => _end;
        readonly Block _body, _end;

        /// <summary>
        /// Reference to the edge defining the enumeree.
        /// </summary>
        public ForeachEnumereeEdge EnumereeEdge => _enumereeEdge;
        readonly ForeachEnumereeEdge _enumereeEdge;

        /// <summary>
        /// Variable to store key in (can be null).
        /// </summary>
        public ForeachVar KeyVariable { get { return _keyVariable; } }
        readonly ForeachVar _keyVariable;

        /// <summary>
        /// Variable to store value in
        /// </summary>
        public ForeachVar ValueVariable { get { return _valueVariable; } }
        readonly ForeachVar _valueVariable;

        internal ForeachMoveNextEdge(Block/*!*/source, Block/*!*/body, Block/*!*/end, ForeachEnumereeEdge/*!*/enumereeEdge, ForeachVar keyVar, ForeachVar/*!*/valueVar)
            : base(source)
        {
            Contract.ThrowIfNull(body);
            Contract.ThrowIfNull(end);
            Contract.ThrowIfNull(enumereeEdge);

            _body = body;
            _end = end;
            _enumereeEdge = enumereeEdge;
            _keyVariable = keyVar;
            _valueVariable = valueVar;

            Connect(source);
        }

        public override IEnumerable<Block> Targets
        {
            get { return new Block[] { _body, _end }; }
        }

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor)
        {
            visitor.VisitCFGForeachMoveNextEdge(this);
        }
    }

    /// <summary>
    /// Represents switch edge.
    /// </summary>
    [DebuggerDisplay("SwitchEdge")]
    public sealed class SwitchEdge : Edge
    {
        readonly BoundExpression _switchValue;
        readonly CaseBlock[] _caseBlocks;

        /// <summary>
        /// The expression representing the switch value.
        /// </summary>
        public BoundExpression SwitchValue => _switchValue;

        public override IEnumerable<Block> Targets => _caseBlocks;

        public override bool IsSwitch => true;

        public override CaseBlock[] CaseBlocks => _caseBlocks;

        internal SwitchEdge(Block source, BoundExpression switchValue, CaseBlock[] caseBlocks)
            : base(source)
        {
            Contract.ThrowIfNull(caseBlocks);
            _switchValue = switchValue;
            _caseBlocks = caseBlocks;

            Connect(source);
        }

        /// <summary>
        /// Visits the object by given visitor.
        /// </summary>
        public override void Visit(GraphVisitor visitor)
        {
            visitor.VisitCFGSwitchEdge(this);
        }
    }
}
