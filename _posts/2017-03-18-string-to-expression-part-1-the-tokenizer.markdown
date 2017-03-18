---
layout: post
title:  "String to Expression Part 1: The Tokenizer"
date:   2017-03-18
description: "Converting a String into an Expression tree is not small task, and the first step of this journey is creating a Tokenizer"
---

Recently I had to make rich filtering API that had to handle all the typical query fineries of `AND`s, `OR`s, equality, comparisons and functions. Naturally I did what any good developer would do, I went and found a library that did it all for me and moved onto more important things.

But it did make me pause for a bit and consider, how would I implmented that myself, would I be able make a simple compiler to convert a string into a .NET expression tree. This series of posts is me answering that question


Hopefull by the end of of this we can convert some like

`"( 2 + 3 ) * 6 + 2"` into an `Exprssion<Func<int>>`


or with only configuration chagne convert


`"birthdate > 1987-02-19 and Name = 'Henry'"` into an `Expression<Func<Person,bool>>`




## The Tokenizer - A less pretentious Lexer

All experts agree the first step to a compiler is a Tokenizer (some of the more sophiscated prefer to call it a Lexer). A Tokenizer has one simple goal, to take in a string 

{% highlight js %}
"( 2 + 3 ) * 6"
{% endhighlight %}

and output a list of labelled symbols we'll call Tokens

{% highlight js %}
[
    "OPENBRACKET (",
    "NUMBER 3",
    "ADD +",
    "NUMBER 3",
    "CLOSEBRACKET )",
    "MULTIPLEY *",
    "NUMBER 6",
]
{% endhighlight %}

the tokenizer has no idea what these symbols mean or how to use them, it simply does the messy string parsing so we only have to think about a list of Tokens. 



Thankfully for us there is core library in almost all programming languages that is designed to make string parsing easier, for this task we are using regular expressions.


## Domain objects

Just so everyone is on the same page here are the objects we will be dealing with

`TokenDefinition` allows us to configure what parts of the string we are interested in, we give it a name and a regex.
{% highlight csharp %}
/// <summary>
/// Defines how a single token is behaves wihtin the system
/// </summary>
public class TokenDefinition
{
    /// <summary>
    /// Name of the definition
    /// </summary>
    public readonly string Name;

    /// <summary>
    /// Regex to match tokens
    /// </summary>
    public readonly string Regex;

    /// <summary>
    /// Indicates whether this token should be ignored during tokenization
    /// </summary>
    public readonly bool Ignore;

    public TokenDefinition(string name, string regex, bool ignore = false)
    {
        this.Name = name;
        this.Regex = regex;
        this.Ignore = ignore;
    }
}
{% endhighlight %}

`Token` is a single piece of string, it has a link back to the definition that created and the value of the match

{% highlight csharp %}
/// <summary>
/// An indivdual piece of the complete input
/// </summary>
public class Token
{
    /// <summary>
    /// The Type of token and how it is defined
    /// </summary>
    public readonly TokenDefinition Definition;

    /// <summary>
    /// The value of the token
    /// </summary>
    public readonly string Value;
    
    public Token(TokenDefinition definition, string value)
    {
        this.Definition = definition;
        this.Value = value;
    }
}
{% endhighlight %}

`Tokenizer` is where all the work is done, given a bunch of tokenDefinitions and a string itll output a list of tokens

{% highlight csharp %}
public class Tokenizer
{

    public Tokenizer(params TokenDefinition[] tokenDefinitions)
    {
        //TODO: Keep reading details to come
    }

    /// <summary>
    /// Convert text into a stream of tokens
    /// </summary>
    /// <param name="text">text to tokenize</param>
    /// <returns>stream of tokens</returns>
    public IEnumerable<Token> Tokenize(string text)
    {
        //TODO: Keep reading details to come

    }
}
{% endhighlight %}

So after we finishing putting it together we expect to be able to use it like

{% highlight csharp %}
var tokenizer = new Tokenizer(
    new TokenDefinition(name:"PLUS", regex: @"\+"),
    new TokenDefinition(name:"MULTIPLY", regex: @"\*"),
    new TokenDefinition(name:"OPENBRACKET", regex: @"\("),
    new TokenDefinition(name:"CLOSEBRACKET", regex: @"\)"),
    new TokenDefinition(name:"NUMBER", regex: @"\d*\.?\d+?"),
    new TokenDefinition(name: "WHITESPACE", regex: @"\s+", ignore: true)
    );

var tokens = tokenizer.Tokenize("( 2 + 3 ) * 6").ToList();

Assert.AreEqual("OPENBRACKET", tokens[0].Definition.Name);
Assert.AreEqual("NUMBER (2)", $"{tokens[1].Definition.Name} ({token[1].Value})");
Assert.AreEqual("PLUS", tokens[2].Definition.Name);
Assert.AreEqual("NUMBER (3)", $"{tokens[3].Definition.Name} ({token[3].Value})");
Assert.AreEqual("CLOSEBRACKET", tokens[4].Definition.Name);
Assert.AreEqual("MULTIPLY", tokens[5].Definition.Name);
Assert.AreEqual("NUMBER (6)", $"{tokens[6].Definition.Name} ({token[6].Value})");

{% endhighlight %}

## Filling in the gaps

With all the infrastructure in place what we need to do is flesh out our `Tokenizer`. Our handling of the token definitions is going to be very simple; take all the regexs, make them all named captures and throw them into one giant regular expression

{% highlight csharp %}
...

/// <summary>
/// Configuration of the tokens
/// </summary>
public readonly IReadOnlyList<GrammerDefinition> TokenDefinitions;

/// <summary>
/// Regex to identify tokens
/// </summary>
protected readonly Regex TokenRegex;
    
public Tokenizer(params TokenDefinition[] tokenDefinitions)
{
    this.TokenDefinitions = tokenDefinitions.ToList().AsReadOnly();
 
    var pattern = string.Join("|", TokenDefinitions.Select(x => $"(?<{x.Name}>{x.Regex})"));
    this.TokenRegex = new Regex(pattern);
}

...
{% endhighlight %}

Now every match of that regular expression will be a single token. 

We can work out which token definition it matched by looking at the capture's name.

{% highlight csharp %}
...

/// <summary>
/// Convert text into a stream of tokens
/// </summary>
/// <param name="text">text to tokenize</param>
/// <returns>stream of tokens</returns>
public IEnumerable<Token> Tokenize(string text)
{
    var matches = TokenRegex.Matches(text).OfType<Match>();

    foreach (var match in matches)
    {
        //token definition we want is the first one that had a successful match
        var matchedTokenDefinition = TokenDefinitions.FirstOrDefault(x => match.Groups[x.Name].Success);
        
        //we wont return ignored tokens
        if (matchedTokenDefinition.Ignore)
            continue;

        yield return new Token(
            definition: matchedTokenDefinition,
            value: match.Value);
    };

}

...
{% endhighlight %}

You may have also noted we add an ignore flag on our tokens so we can remove annoying things such as whitespace during tokenization. Ignored tokens are matched but never returend. 

Also if you follow the logic you'll see in the odd case where multiple TokenDefinitions match the same token we just take the first TokenDefinition's match. This does mean that order of your `TokenDefinition`s matter, so you will need to define the more specific definitions before the broader ones. 

And with very minimal code we have a fully functional, and configurable, `Tokenizer`

## What is next

As awesome as our tokenizer is, all we have done is converted the problem from "converting a string into an Exprsesions" to the problem of "converting a list of tokens into an expression". It is small step, but one in the right direction. The larger problem is for our `Parser` to solve, which is what we are going to write in the next post.


## Full code
The full code of the entire parser is can be found at [https://github.com/codecutout/StringToExpression](https://github.com/codecutout/StringToExpression)

The code for the `Tokenizer` is is at [https://github.com/codecutout/StringToExpression/tree/master/StringToExpression/Tokenizer](https://github.com/codecutout/StringToExpression/tree/master/StringToExpression/Tokenizer). It may not look exactly like the excerpts on this post, because I omitted some of the error checking code in this post, but the idea is the same.







