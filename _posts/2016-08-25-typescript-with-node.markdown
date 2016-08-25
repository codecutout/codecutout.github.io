---
layout: post
title:  "Setting up the node+typescript environment"
date:   2016-08-25
description: "I love typescript, but found setting up a new project to be more daunting than it should. Here is just a simple setup to get Node, Typescript and Typings all working together"
---

Typescript and node work well together, we get the simpleness and power of node, but with the comfort of having type checking and intellisense. 

However the hardest thing ive found with typescript was setting up that first project. Configuring the build pipeline, knowing how to reference other code and how to handle definition files all add complexity to this otherwise great pairing. Im not saying all this information is not available, its usually quite fragmented and outdated.

So here is my node+typescript setup


## File Structure
My project file structure usually looks something like this. There is no defacto standard in nodeland so personal preference reigns, but here is what we are going for

```
|-- my-project
	|-- node_modules
		|-- [nodes dependeancies, npm will build this out]
    |-- dist
    |   |-- [all the transpiled .js code and their .map files]
    |-- src
    |   |-- [all my source typescript code]
    |-- typings
        |-- [all the stuff generated typings]
	|-- package.json
    |-- tsconfig.json
    |-- typings.json
```


## Install the things we need
If you havent already go [install node](https://nodejs.org/en/download/). Also save yourself some CLI dependancy headaches and go add the following to your path 

{%highlight shell%}
./node_modules/.bin
{%endhighlight%}

Create a project file and get your shell/command prompt ready

{%highlight shell%}

# Create an NPM project, it will ask you some questions default is fine for most of it
$> npm init

# Install our typescript compiler
$> npm install typescript --save-dev 

# Install Typings, our typescript definition manager
$> npm install typings --save-dev 

# Let Typings create its config file
$> typings init

{%endhighlight%}


## Pull in our definitions
Typescript is really only useful if the system knows all the metadata about the types we will use. Typings is our access to all of this metadata. Because we are using node we want to let Typescript know all the node global properties are available. So you can run

{%highlight shell%}

#Tells the typescript compiler that we have access to all the power of node
$> typings install env~node --global --save

{%endhighlight%}

## Configure our Typescript compiler
Originally the typescript compiler (tsc) had a bunch of command line flags that had to be set, which made our build scripts rather complicated, fortunatly there is now a `tsconfig.json` file you can put in the root directory to define compiler options, this is great! it means the compiler options are source controlled and consistant across different enviornments.

So create a `tsconfig.json` in the root directory and put this in it. You might need to adjust the version or compiler options to fit, but this will do for our basic needs

{%highlight json%}
{
    "version": "1.8.10",
    "compilerOptions": {
        "target": "es5",
        "module": "commonjs",
        "sourceMap": true,
		"outDir": "dist"
    },
    "exclude": [
        "node_modules",
        "dist"
    ]
}
{%endhighlight%}


## Simple Typescript file and command line compiled

Now lets write some source. Nothing fancy. Create a file in `src/start.ts` with the following

{%highlight javascript%}
console.log("Hello World!")
{%endhighlight%}

Now in the console run

{%highlight shell%}
$> tsc
{%endhighlight%}

You should now have a `start.js` in your dist folder that you can run

{%highlight shell%}
$> node dist/start
{%endhighlight%}

and we have done it, built use typescript and node

## Automate it

However, no sane developer will be running `tsc` on the command line every time they make a change, so lets automate this a bit. We could use Grunt or Gulp but my preference is to do our builds in a few simple npm script commands (even on larger projects I tend to opt for npm scripts over Grunt Gulp as well).

Inside your package.json there should be a `scripts` section, our `npm init` probably already created a "test" stub, we are going to add a few more

{%highlight json%}
{
  ...
  "scripts": {
    "test": "echo \"Error: no test specified\" && exit 1",
    "build": "tsc",
    "build:watch": "tsc --watch",
    "start": "node dist/start"
  },
  ...
}
{%endhighlight%}

These scripts are basically aliases to the shell commands. This means we can now build our application by running (This is what your build servers should be running)

{%highlight shell%}
$> npm build
{%endhighlight%}

But for development it means we can run
{%highlight shell%}
npm run build:watch
{%endhighlight%}

This will start a long running process that will monitor all changes in your `.ts` files and build whenever they change, it will also show the errors on the console whenever you mess up.

The reason we put these into our package.json is that when our built becomes more compliated we can just update our source controlled `package.json` file rather than updating any server that is using it.

## IDE Support

I can only speak for WebStorm (although there is supposedly good support in visual studio and [atom](https://atom.io/packages/atom-typescript),

My ideal IDE would allow me to run `npm build:watch` then it would interpret the output as it happens and highlight the errors for me. Unfortunatly WebStorm does not do this, what it does do is runs its own tsc command in the background independant of my npm scripts, it will then highlight errors based on its own compilation. When using WebStorm you will not need to ever run `npm run build:watch`

This is very easy to get running (File->Settings) and for the most part it is well supported. 

![webstorm typescript settings](/assets/posts/img/webstorm-typescript.jpg)


There are a few things that you should keep in mind though

* WebStorms build process is not the same as your build servers build process. WebStorm is doing its own typescript transpilation, while your build server will likely be running `npm build`. This has high potential for "it works on my machine"
* WebStorm runs a tsc from a bundled version, and wont let you use the one in your node_modules. This means your versions used in your `npm build` that your build server uses might not match those used in your dev enviornment


## More dependancies

We got our basic build chain going above, we can now add and edit typescript files and change are automatically transpiled and put in our `dist` directory.

The last thing is adding new dependancies and their associated type definitions. So we will expand our simple app to use express.

we will use node to install express
{%highlight shell%}
$> npm install express --save
{%endhighlight%}

And we will update our start.ts to be an express webserver

{%highlight typescript%}
import * as express from 'express';

var app = express();

app.get('/', (req,res)=>{
    res.send("Hello World!")
});

app.listen(process.env.PORT || 3000);
{%endhighlight%}



At this point you will probably recieve some typescript errors along the lines of `Cannot find module 'express'` this is because typescript has no idea what is in this module. Counter intuitively the code will still run as node knows how to resolve express but we lose all the benefits of typescript

So we need to let typescript know that 'express' exists and give it all the metadata it needs to work with express. This is done through Typings, run the command

{%highlight typescript%}
$> typings install dt~express --global --save
{%endhighlight%}

There is a lot of flags on that command, so a quick breakdown of what they all mean. 

* `dt~` - tells it to look in the [Definitely Typed](https://github.com/DefinitelyTyped/DefinitelyTyped) repository for the metadata. Definitely Typed is a project that contains typescript definitions, it is still the the most comprehensive source for typescript metadata, all common libraries will have their metadata in there somewhere. 
* `--global` flag lets you know the definitions have their name hardcoded, in our expample our definition is tied with the name 'express' and you cant do anything to change it. The flag is you accepting this fact rather than telling Typings to do anything different. Hardcoding names used to be the only way to write typescript definitions so most of the available defintions still have this restrictions
* `--save` just like in npm it lets typings know you should remember this so you can install them later

Personally im not a fan of Typings, Its commands are long winded for the common (albeit legacy) case. Its documenation does little to advice on its recommended usage and it has a very generic name so it is difficult to google help. Unfortunatly its the tool we are stuck with, at least until typescript definitions bundled alongside node modules become standard.

But rants aside, our typescript now knows what express is everything compiles and runs. Just remember when you add new libraries you need to install both the library (via `npm install`) and the defintions (via `typings install`).

## Summing up

Starting up a project

* `npm init`
* `npm install typescript --save-dev`
* `npm install typings --save-dev`
* `typings init`
* `typings install env~node --global --save`
* Add a `tsconfig.json` file
* Add a `"build":"tsc"` script to `package.config`
* Add a `"build:watch":"tsc --watch" script to `package.config`

When adding more libraries

* `npm install my-cool-lib --save`
* `typings dt~my-cool-lib --global --save`



















