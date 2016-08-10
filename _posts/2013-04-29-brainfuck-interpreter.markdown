---
layout: post
title:  "Brainfuck Interpreter"
date:  2013-04-29
description: "Tinkering with F# to make a Brainfuck Interpreter"
redirect_from: "/brainfuck-interpreter"
---

As an excuse to tinker with F# I thought to try my hand at writing a Brainfuck interpreter in F#. It may not be ground breaking code, but it does provide an interesting look into F#

[The full solution][1]

## F what?
F# is a functional programming language that compiles into the MSIL (the same stuff C# compiles into). Being a functional language the code and subsequently the way you approach the problem are different. If you are looking for something different but don't want to leave the comfort of .NET, F# is worth a look.

## Brain what?
Brainfuck is a very simple language which contains only six keywords (actually key characters) which are

 - `>`	increment the data pointer
 - `<`	decrement the data pointer
 - `+`	increment the byte at the data pointer
 - `-`	decrement the byte at the data pointer
 - `.`	output the byte at the data pointer
 - `,`	accept one byte of input, storing its value in the byte at the data pointer
 - `[`	if the byte at the data pointer is zero, then jump to the corresponding ].
 - `]`	if the byte at the data pointer is nonzero, then jump back to the corresponding [.

Any other character is ignored when processing the file

This does mean applications tend to be completely unreadable (hence the name), straight from Wikipedia the basic hello world program looks like

{% highlight c %}
+++++ +++++             initialize counter (cell #0) to 10
[                       use loop to set the next four cells to 70/100/30/10
	> +++++ ++              add  7 to cell #1
	> +++++ +++++           add 10 to cell #2 
	> +++                   add  3 to cell #3
	> +                     add  1 to cell #4
	<<<< -                  decrement counter (cell #0)
]                   
> ++ .                  print 'H'
> + .                   print 'e'
+++++ ++ .              print 'l'
.                       print 'l'
+++ .                   print 'o'
> ++ .                  print ' '
<< +++++ +++++ +++++ .  print 'W'
> .                     print 'o'
+++ .                   print 'r'
----- - .               print 'l'
----- --- .             print 'd'
> + .                   print '!'
> .                     print '\n'
{% endhighlight %}

It may not look pretty but its the simple syntax which provides just the right amount of scope to write a not too complicated interpreter and is a perfect excuse to play with F#

## The interpreter
Here is the final code for my interpreter in all its glory, it runs as a command line tool

{% highlight csharp %}
#light
module Main =
	open System
	open System.IO
	open System.Text

	type BrainfuckMachine (memSize) = 
		let mutable _ptr = 0
		let _memory = Array.zeroCreate<byte> memSize

		member this.Ptr 
			with get () = _ptr
			and set v = _ptr <- v 

		member this.Memory
			with get() = _memory

		member this.Value
			with get() = _memory.[_ptr]
			and set v = _memory.[_ptr] <- v


	let execute (filename:string) =
		//The sequence of characters from the file
		let charSeq = seq { 
			use sr = new StreamReader(filename);
			while not sr.EndOfStream do
				yield! sr.ReadLine().ToCharArray()
		}

		//helper method to allow a sequence given an enumerator. This allows
		//sequences to share a common enumerator but have the benefit of being
		//a sequence
		let sharedSeq (enumerator:Collections.Generic.IEnumerator<'a>) = seq {
			while enumerator.MoveNext () do
				yield enumerator.Current
		}

   

	
		//Converts the character sequence into a sequence of functions
		//The entire while block is considered one function
		let rec instructionSeq (instructions:seq<char>) = seq {
			let enumerator = instructions.GetEnumerator()
		
			while enumerator.MoveNext () && enumerator.Current <> ']' do
				yield match enumerator.Current with
					| '>' -> fun (m:BrainfuckMachine) -> m.Ptr <- m.Ptr + 1
					| '<' -> fun (m:BrainfuckMachine) -> m.Ptr <- m.Ptr - 1
					| '+' -> fun (m:BrainfuckMachine) -> m.Value <-  m.Value + byte 1
					| '-' -> fun (m:BrainfuckMachine) -> m.Value <- m.Value - byte 1
					| '.' -> fun (m:BrainfuckMachine) -> Encoding.ASCII.GetChars([|m.Value|]).[0] |> Console.Write
					| ',' -> fun (m:BrainfuckMachine) -> m.Value <- Encoding.ASCII.GetBytes([|Console.ReadKey().KeyChar|]).[0]
					| '[' -> 
						//gather up all the inner items to be executed later
						let block = enumerator |> sharedSeq |> instructionSeq |> Seq.toList
						fun(m:BrainfuckMachine) ->
					
							while m.Value <> byte 0 do
								block |> Seq.iter (fun (f)->f m)
					| _ -> fun (m:BrainfuckMachine) -> () //the only reason yield! is used so so we can return nothing here
		  
		}

		//create a machine with some memory
		let BrainfuckMachine = new BrainfuckMachine 30000

		//pipe our sequence of characters to get a sequence of methods and execute each method in turn
		charSeq |> instructionSeq |> Seq.iter (fun (f)->f BrainfuckMachine)    

		//hold the screen so we can see some result
		Console.WriteLine ()
		Console.WriteLine "press any key to exit"
		Console.ReadKey() |> ignore
		0




	[<EntryPoint>]
	let main (args:string[]) =
		match args with
		| [|_|] ->
		   execute args.[0] 
		| _ ->
			Console.WriteLine("Usage: BrainfuckInterpreter.exe [BrainfuckFile.b]")
			-1    
{% endhighlight %}



I don't want this post to turn into an F# tutorials, there are already plenty of those around written by people with loads more F# experience than me. But I will touch on items that left an impression - both good and bad


## Less bulk
F# does a very good job at feeling lightweight. With its type inference, pattern matching, tabs instead of structural brackets it does a lot to try and keep your code short and to the point.

## Cursing Recursion
Being a functional langauge recursion plays a big part, and can give you some very elegant solutions in a small codebase. But unfortunately recursion is a pain to debug. The majority of the time writing the interpreter was trying to debug an issue with a recursive call.


## Yield Bang!
In C# I am an Enumerable junkie and was ecstatic to find out F# has something similar in sequences. However what I was very surprised to find is that as well as supporting the `yield` keyword they also have another `yield!` keyword. 

Running `yeild! [somelist]` will yield each individual element in the list, essentially flattening it out. 

so `yield! [1,2,3,4]` is the same as `foreach(var a in [1,2,3,4]) { yield a }`


## Piping 
Being an enumerable junkie I'm also fond of LINQ and chaining expressions together. Turns out F# has something just like that in `|>`. It allows the first element to be passed as a parameter to the second.

so `a |> b` is the same as `b(a())` and `a |> b |> c` would be the same as `c(b(a()))`

You can already see from the example how nicer that makes your code look 


## Controlling flow
An issue which I kept running into was getting limited in the ways I can control the flow of the program. 

For example when running my interpreter I wanted to check some inputs and return early if I'm missing a parameter, something like

    if args.Length <> 1
       Console.WriteLine("Usage: BrainfuckInterpreter.exe [BrainfuckFile.b]")
 	  return -1
    //All the rest of the code not any any if or else block

Except F# has no `return` word (it will consider the last expression in the function to be the return value). The obvious workaround is just to use an else statement, but that does add an additional indent to my code id rather not have.

I also ran into similar flow issues when handling non-keyword characters. I wanted to just ignore them, but was forced to have something so my expression would return a value. I had to resort to return an empty function to keep the compiler happy 


## Mutability
F# is big on immutability, variables are by default constant (making them not variable at all, F# just calls them values). This actually works out great when you are writing self contained computationally complex code. 

However my interpreter had to cross the immutable line. Due to the requirement of allocating a big array of bytes to act as the brainfuck memory I could not really get around it (easily), I was emulating a mutable brainfuck machine.

You might consider an interpreter a special case to break the immutability rule, but with an abundance of .NET libraries built on mutable code I suspect you will always have mutable values littering your F#.


## Miles to go before I sleep
There are loads of other great feature in F# that were not mentioned here, primarily because I could not find a place to use them in my interpreter, in particular features like Discriminated Unions, Unit of Measures, Type Providers and Asynchronous Programming. 

Now I just need to think of another F# project that can encapsulate the things I missed.


  [1]: assets/posts/code/BrainfuckInterpreter/BrainfuckInterpreter.zip