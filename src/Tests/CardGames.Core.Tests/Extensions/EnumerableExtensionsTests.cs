using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.Extensions;
using FluentAssertions;
using Xunit;

namespace CardGames.Core.Tests.Extensions;

public class EnumerableExtensionsTests
{
    [Fact]
    public void ForEach_Executes_Action_For_Every_Item()
    {
        var source = new[] { 1, 2, 3 };
        var result = new List<int>();

        source.ForEach(item => result.Add(item));

        result.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void ForEach_With_Index_Executes_Action_With_Correct_Indices()
    {
        var source = new[] { "a", "b", "c" };
        var result = new Dictionary<int, string>();

        source.ForEach((item, index) => result.Add(index, item));

        result.Should().ContainKey(0).WhoseValue.Should().Be("a");
        result.Should().ContainKey(1).WhoseValue.Should().Be("b");
        result.Should().ContainKey(2).WhoseValue.Should().Be("c");
    }

    [Fact]
    public void Subsets_Returns_All_Subsets()
    {
        var source = new[] { 1, 2 };
        // Subsets of {1, 2} should be 2^2 = 4 subsets
        var result = source.Subsets().ToList();
        
        result.Should().HaveCount(4);
        result.Select(x => x.ToList()).Skip(0).Should().ContainEquivalentOf(new List<int>());
        result.Select(x => x.ToList()).Skip(0).Should().ContainEquivalentOf(new List<int> { 1 });
        result.Select(x => x.ToList()).Skip(0).Should().ContainEquivalentOf(new List<int> { 2 });
        result.Select(x => x.ToList()).Skip(0).Should().ContainEquivalentOf(new List<int> { 1, 2 });
    }

    [Fact]
    public void Subsets_Throws_When_Source_Is_Null()
    {
        IEnumerable<int> source = null;
        Action act = () => source.Subsets();
        act.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void Subsets_Returns_Only_Empty_Set_When_Source_Is_Empty()
    {
        var source = Enumerable.Empty<int>();
        var result = source.Subsets().ToList();
        result.Should().HaveCount(1);
        result.First().Should().BeEmpty();
    }

    [Fact]
    public void SubsetsOfSize_Returns_Subsets_Of_Correct_Size()
    {
        var source = new[] { 1, 2, 3 };
        var result = source.SubsetsOfSize(2).ToList();
        
        result.Should().HaveCount(3);
        result.Select(x => x.OrderBy(i => i)).Should().ContainEquivalentOf(new[] { 1, 2 });
        result.Select(x => x.OrderBy(i => i)).Should().ContainEquivalentOf(new[] { 1, 3 });
        result.Select(x => x.OrderBy(i => i)).Should().ContainEquivalentOf(new[] { 2, 3 });
    }

    [Fact]
    public void SubsetsOfSize_Throws_When_Source_Is_Null()
    {
        IEnumerable<int> source = null;
        Action act = () => source.SubsetsOfSize(2);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SubsetsOfSize_Returns_Empty_For_Invalid_Size(int size)
    {
         var source = new[] { 1, 2 };
         var result = source.SubsetsOfSize(size);
         result.Should().BeEmpty();
    }
    
    [Fact]
    public void SubsetsOfSize_Returns_Empty_When_Size_Larger_Than_Count()
    {
         var source = new[] { 1, 2 };
         var result = source.SubsetsOfSize(5);
         result.Should().BeEmpty();
    }
    
    [Fact]
    public void SubsetsOfSize_Returns_Source_When_Size_Equals_Count()
    {
         var source = new[] { 1, 2 };
         var result = source.SubsetsOfSize(2).ToList();
         result.Should().HaveCount(1);
         result.First().Should().BeEquivalentTo(source);
    }
    
    [Fact]
    public void SubsetsOfSize_Returns_List_Of_Single_Items_When_Size_Is_One()
    {
         var source = new[] { 1, 2 };
         var result = source.SubsetsOfSize(1).ToList();
         result.Should().HaveCount(2);
         result.Should().ContainEquivalentOf(new[] { 1 });
         result.Should().ContainEquivalentOf(new[] { 2 });
    }

    [Fact]
    public void CartesianProduct_Combines_Two_Sequences()
    {
        var setA = new[] { 1, 2 };
        var setB = new[] { 3, 4 };
        
        var result = setA.CartesianProduct(setB).ToList();
        result.Should().HaveCount(4);
        result.Should().ContainEquivalentOf(new[] { 1, 3 });
        result.Should().ContainEquivalentOf(new[] { 1, 4 });
        result.Should().ContainEquivalentOf(new[] { 2, 3 });
        result.Should().ContainEquivalentOf(new[] { 2, 4 });
    }

    [Fact]
    public void CartesianProduct_Combines_Sequence_With_List_Of_Sequences()
    {
        var setA = new[] { 1, 2 };
        var sequences = new[] { new[] { 3, 4 }, new[] { 5, 6 } };
        
        var result = setA.CartesianProduct(sequences).ToList();
        result.Should().HaveCount(4);
        result.Should().ContainEquivalentOf(new[] { 1, 3, 4 });
        result.Should().ContainEquivalentOf(new[] { 1, 5, 6 });
        result.Should().ContainEquivalentOf(new[] { 2, 3, 4 });
        result.Should().ContainEquivalentOf(new[] { 2, 5, 6 });
    }

    [Fact]
    public void CartesianPower_Returns_Combinations_Of_Length_Power()
    {
        var source = new[] { 1, 2 };
        var power = 2;
        
        var result = source.CartesianPower(power).ToList();
        result.Should().HaveCount(4);
        result.Should().ContainEquivalentOf(new[] { 1, 1 });
        result.Should().ContainEquivalentOf(new[] { 1, 2 });
        result.Should().ContainEquivalentOf(new[] { 2, 1 });
        result.Should().ContainEquivalentOf(new[] { 2, 2 });
    }
    
    [Fact]
    public void CartesianPower_Throws_For_Negative_Power()
    {
        var source = new[] { 1 };
        Action act = () => source.CartesianPower(-1);
        act.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void CartesianPower_Returns_Empty_For_Zero_Power()
    {
        var source = new[] { 1, 2 };
        var result = source.CartesianPower(0);
        result.Should().BeEmpty();
    }
}
