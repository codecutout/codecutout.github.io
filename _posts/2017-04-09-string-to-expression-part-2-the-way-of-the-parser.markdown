---
layout: post
title:  "String to Expression Part 2: The way of the parser"
date:   2017-04-09
description: "Converting tokens into an expression"
---

The [previous part](/blog/string-to-expression-part-1-the-tokenizer/) we built a a tokenizer. Now we need to get into the meat of our project, parsing tokens into an expression

## The plan - shunting yard algorithm

Normally I'm for just writing code and seeing what comes out, however for something as involved as this we need to have a plan, and that plan is the [Shunting Yard Algorithm](https://en.wikipedia.org/wiki/Shunting-yard_algorithm).

It turns out we can approach our problem the same way railway shunting yards use tracks to separate railway cars. If (like the majority of developers) that analogy means nothing to you, don't worry the algorithm is quite simple.


### Step 1: Applying the tokens
Firstly we will run forward through all our tokens and classify each token and put them in one of two stacks, a token can either be:
* An Operand - An expression that has (or can be evaluated to) a constant. For example `2`, `'mystring'`, `x.Value`, `2+3` are all operands.
* An Operator - is something that will act on one or more operands and as will create a new operand as a result. For example `+`, `*`, `toLower()` are all operators.

For example if we are processing a bunch of tokens like this
{% highlight js %}
[ "3", "+", "5", "*", "6"]
{% endhighlight %}

after we classify our two stacks look something like this

Operand Stack | Operator Stack
---|---
3 | +
5 | *
6 | 

### Step 2: Pop operations and execute them

Now that we have applied the tokens we have everything in one of two stacks. The next step is to starting pop'ing items off our operation stack, we will then execute them. When we execute them they will pop all the operands they need from the operand stack combine them and push the result back onto the operand stack.

Given our example 

Operand Stack | Operator Stack
---|---
3 | +
5 | *
6 | 

We would then 
1. Pop `*` off the operator stack. 
2. Execute the multiplication, to do this it needs two operands which we pop from the operand stack, so it would pop `6` then `5`
3. The result of the multiplication is the operand `(6*5)` (remember an operand can be anything that can be evaluated to a constant so it can be a combination of other operands). We would then push this back on the operand stack

So after our first operator execution our stacks look like 

Operand Stack | Operator Stack
---|---
3 | +
(6*5) | 

Our next step is to continue pop'ing things off the opertor stack and executing just as before. So the next `+` is popped off the operation stack, it in turn pops two operands from the operand stack combines them together to a new operand which it puts back on the stack. This leaves us with

Operand Stack | Operator Stack
---|---
((6*5) + 3) | 

When we run out of operators we should (if our input was a valid expression) only have a single operand left, that single operand is our final expression.

### Other features
It should be noted there are a few additional processing add-ons we can use to make it possible to handle things like
* Order of operations - for example multiplication should always happen before addition)
* Brackets - bracketed expressions should happen first
* Functions - which take in variable number of operands

Ill discuss these add-ons in the next post, for now we will just get the basic infrastructure in place.

## Being practical

### Modeling the problem

So what does the Shunting Yard algorithm mean in our context? We will model it as
* A `ParseState` which will hold our stack of operands and operators.
* An `Operand` which simply has an `Expressionn`. It might be a `ConstantExpression` or a `MemberExpression` or be the root for a tree of an expression such as a `BinaryExpression`
* An `Operator` which contains an `Action`. This action will pop some `Operands` from the `ParseState` create a new `Expression` using the `Operands` and push the new `Expression` back on the `Operand` stack. Exactly how many operands it pops and what expression it creates is dependent on the type of `Operator`.

{% highlight csharp %}
public class ParseState
{
    public Stack<Operand> Operands { get; } = new Stack<Operand>();

    public Stack<Operator> Operators { get; } = new Stack<Operator>();
}
{% endhighlight %}

{% highlight csharp %}
public class Operand
{
    public readonly Expression Expression;

    public Operand(Expression expression)
    {
        this.Expression = expression;
    }
}
{% endhighlight %}

{% highlight csharp %}
public class Operator
{
    public readonly Action Execute;

    public Operator(Action execute)
    {
        this.Execute = execute;
    }
}
{% endhighlight %}

### More TokenDefintions

We now need to run through our token list and classify the tokens as either an `Operand` or `Operator`. The best place to determine what type of token we are, is where we defined the token to begin with, at the `TokenDefinition`. We will add a virtual Apply method with the intention we can implement the specific in subclasses.

{% highlight csharp %}
public class TokenDefinition
{
    public readonly string Name;

    public readonly string Regex;

    public readonly bool Ignore;

    public TokenDefinition(string name, string regex, bool ignore = false)
    {
        this.Name = name;
        this.Regex = regex;
        this.Ignore = ignore;
    }

    public virtual void Apply(Token token, ParseState state)
    {
        //This is our new method
    }
}
{% endhighlight %}


Firstly we will make subclass of `OperandDefintion` which will know how to turn the token string into an Expression, it will `Apply` this knowledge to add the item to the `Operand` stack.

{% highlight csharp %}
public class OperandDefinition : TokenDefinition
{
    public readonly Func<string, Expression> ExpressionBuilder;

    public OperandDefinition(string name, string regex, Func<string, Expression> expressionBuilder)
        : this(name, regex)
    {
        ExpressionBuilder = expressionBuilder;
    }

    public override void Apply(Token token, ParseState state)
    {
        //use the expressionBuilder to build the token and push it to the operand stack.
        Expression resultExpression = ExpressionBuilder(token.Value);
        state.Operands.Push(new Operand(resultExpression));
    }
}
{% endhighlight %}

Next we will make a `BinaryOperatorDefinition` to handle our simple operators such as `+`, `*`, `AND` etc. As we make more complicated operators we can simply create new types of `TokenDefinition`. It should be noted that we do not actually execute the `Operator` in the `Apply` method we only create an `Action` so it can be executed later.

{% highlight csharp %}
public class BinaryOperatorDefinition : TokenDefinition
{
    public readonly Func<Expression, Expression, Expression> ExpressionBuilder;

    public BinaryOperatorDefinition(string name,
        string regex,
        Func<Expression, Expression, Expression> expressionBuilder)
        : base(
        name: name,
        regex: regex)
    {
        ExpressionBuilder = expressionBuilder;
    }

    public override void Apply(Token token, ParseState state)
    {
        state.Operands.Push(new Operator(() => {
            var operand1 = state.Operands.Pop();
            var operand2 = state.Operands.Pop();
            var newOperand = ExpressionBuilder(operand1, operand2);
            state.Operands.Push(newOperand)
        }));
    }
}
{% endhighlight %}

### The Parser

The thing remaining is actually calling the apply, which is almost trivial. Ive also thrown in the operator execution loop in there which is equally trivial. We Apply all the tokens, then we pop off the `Operator` stack and execute the operators.

{% highlight csharp %}
public class Parser
{
    public Expression Parse(IEnumerable<Token> tokens)
    {
        var compileState = new ParseState();

        //First pass we apply all our tokens
        foreach (var token in tokens)
        {
            token.Definition.Apply(token, compileState);
        }

        //next pass we pop our operators and execute them
        while (state.Operators.Count > 0)
        {
            var op = state.Operators.Pop();
            op.Execute();
        }

        //after all that we should only have a single operand left, that is our result
        return state.Operands.Peek().Expression;
        
        return outputExpression;
    }
}
{% endhighlight %}

Finally we hook it up with our Tokenizer

{% highlight csharp %}
public class Language
{
    public Language(params GrammerDefinition[] grammerDefintions)
    {
        Tokenizer = new Tokenizer.Tokenizer(grammerDefintions);
        Parser = new Parser();
    }

    public Expression Parse(string text)
    {
        var tokenStream = this.Tokenizer.Tokenize(text);
        var expression = Parser.Parse(tokenStream, parameters);
        return expression;
    }
}
{% endhighlight %}

With all this in place we can configure our entire language via `TokenDefintion` and its subclasses

{% highlight csharp %}
var language = new Language(
    new OperatorDefinition(
        name:"PLUS", 
        regex: @"\+", 
        expressionBuilder: (arg1, arg2) => Expression.Add(arg1, arg2)),
    new OperatorDefinition(
        name:"MULTIPLLY", 
        regex: @"\*", 
        expressionBuilder: (arg1, arg2) => Expression.Multiply(arg1, arg2)),
    new OperandDefinition(
        name:"NUMBER", 
        regex: @"\d*\.?\d+?",
        expressionBuilder: x => Expression.Constant(decimal.Parse(x))),
    new GrammerDefinition(name: "WHITESPACE", regex: @"\s+", ignore: true)
    );

var expression = language.Parse("3+5*6");


{% endhighlight %}


## What is next

We have a working parser it makes expressions we are good to go right? Well we are missing some very critical elements
* order of operations - as it is the expression is evaluated right-to-left, BEMA should apply
* brackets - putting brackets round anything should force that part ot evaluate first
* functions - we are not calling any functions here

But it turns out all of these can be built upon our existing objects, we just need to make new `TokenDefinitions` for them, which is what we will do in part 3.


## Full code
The full code of the entire parser is can be found at [https://github.com/codecutout/StringToExpression](https://github.com/codecutout/StringToExpression)

Some of the code will not appear exactly as it is in this post, mostly because the real code has a lot more error checking, although the principles are the same.







