---
layout: post
title:  "npm say no to '--global' and yes to '--save-dev'"
date:   2016-08-18
description: "npm --install guides
---

There are far too many quick start guides for tools that tell you to install npm packages via

```
#BAD!
npm install --global MyCoolTool
```

What you should be doing is

```
#GOOD
npm install --save-dev MyCoolTool
```

==
installing to global makes it available to all the projects you use on your computer under your profile. This seems like a good idea at first but it locks you into the single version of that tool, and if you have several projects that are only compatible with specific version you'll have problems. Perhaps the bigger issue is that on boarding new developers requires them to also install a bunch of global packages, which in all liklihood you have forgotten what they were and have no documenation of them.

Alternativly installing it to dev dependancies means your project uses the tool version it was meant to, and more importantly new developers are only a single `npm install` away from installing everything they need to start working.



==But i want to access CLI tools

Installing global did have the nice feature that the executables went on your path, this meant after running something like `npm install -g browserify' you could then directly run 'browserify main.js` and your computer new where to find the browserify command. 

But it turns out there is a way to get similiar behaviour and still being able to `npm install --save-dev`. We can simply add the following to your PATH variable
```
./node_modules/.bin
```

Now whenever you try run commands it will check to see if you project has any executable dependancies with the correct and try and run those first.


