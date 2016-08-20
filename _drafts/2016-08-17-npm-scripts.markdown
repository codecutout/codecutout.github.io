---
layout: post
title:  "npm - the task runner you never knew you had"
date:   2014-02-16
description: ""
redirect_from: "/one-project-file"
---

When working in javascript land (both node and frontend) I was always irked by the use of task runners (Grunt, Gulp and their like). They require their own specialised libraries and knowledge and right when about to start a new project is not the time where I want to get side tracked by tooling.


But they seemed like necassary tools if I want to setup a nice automated enviornment to build my Typescript files, compile my SASS and minify my JS, it was the real option I had ... or so I had thought

==The unassuming NPM
NPM is a package manager, and its good at being a package manager, it is not a task runner. But it does have a very simple script runner built in, which as it turns out can be used to do most of the things I care about

With a config like this
```
{
  "name": "piggy-proxy",
  "version": "1.0.0",
  "description": "",
  "main": "piggy-proxy.js",
  "scripts": {
    "test": "mocha --reporter spec --recursive ./test",
    "start": "node src/start",
	"BuildMyTypescript" : "tsc js/main.ts js/main.js" --todo check these
	"SassMyStylesheets":"node-sass sass/main.scss css/main.css",
	"MinifyMyJavascripts":"nody-minify js/main.js js/main.min.js"
  }
}
```

I can write in teh command line

`$> npm run BuildMyTypescript`

and it will run the command `tsc js/main.ts js/main.js`. The script commands can make full use of any shell commands that are in your project dependancies, they can also use all the normal shell pipings and ()

To help normalize the names there are a few special script names (start, stop, test, build) that you can run with the shortcut

`$> npm start`
`$> npm stop`
`$> npm test`
`$> npm build`


YOou  might just be thinking npm scripts are simply command aliases, and you are right. Things start to be more interesting when you chain multiple together

==npm-run-all

npm-run-all is a simple  



