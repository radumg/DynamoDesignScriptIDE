﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Dynamo.Utilities;
using ProtoCore.AST.AssociativeAST;

namespace CoreNodeModels
{
    public abstract class EnumAsInt<T> : EnumBase<T>
    {
        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            var rhs = AstFactory.BuildIntNode(SelectedIndex);
            var assignment = AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), rhs);

            return new[] { assignment };
        }
    }

    public abstract class EnumAsString<T> : EnumBase<T>
    {
        public override IEnumerable<AssociativeNode> BuildOutputAst(List<AssociativeNode> inputAstNodes)
        {
            var rhs = AstFactory.BuildStringNode(Items[SelectedIndex].Item.ToString());
            var assignment = AstFactory.BuildAssignment(GetAstIdentifierForOutputIndex(0), rhs);

            return new[] { assignment };
        }
    }

    public abstract class EnumBase<T> : DSDropDownBase
    {
        protected EnumBase() : base(typeof(T).ToString()) { }

        public override void PopulateItems()
        {
            Items.Clear();
            foreach (var constant in Enum.GetValues(typeof(T)))
            {
                Items.Add(new DynamoDropDownItem(constant.ToString(), constant));
            }

            Items = Items.OrderBy(x => x.Name).ToObservableCollection();
        }
    }

    /// <summary>
    /// The drop down node base class which lists all loaded types which are children
    /// of the provided type.
    /// </summary>
    public abstract class AllChildrenOfType<T> : DSDropDownBase
    {
        protected AllChildrenOfType() : base("Types")
        {
            RegisterAllPorts();
        }

        public override void PopulateItems()
        {
            Items.Clear();

            var childTypes = typeof(T).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(T)));

            foreach (var childType in childTypes)
            {
                Debug.WriteLine(childType);
                var simpleName = childType.ToString().Split('.').LastOrDefault();
                Items.Add(new DynamoDropDownItem(simpleName, childType));
            }

            Items = Items.OrderBy(x => x.Name).ToObservableCollection();
        }
    }
}
