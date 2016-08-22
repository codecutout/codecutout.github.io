---
layout: post
title:  "npm install --global is not the way"
date:   2016-08-18
description: "Installing npm packages globally is bad advice, --save-dev is the better alternative"
---

There are far too many quick start guides for tools that tell you to install npm packages globally

{% highlight shell %}
#BAD!
npm install --global MyCoolTool
{% endhighlight %}

However this is bad advice, what you should be doing is

{% highlight shell %}
#GOOD!
npm install --save-dev MyCoolTool
{% endhighlight %}

## Contain those dependancies
installing to global makes it available to all the projects you use on your computer under your profile. This seems like a good idea at first but it locks you into the single version of that tool, and if you have several projects that are only compatible with specific version you'll have problems. Perhaps the bigger issue is that on boarding new developers requires them to also install a bunch of global packages, which in all liklihood you will have no memory of what the needed packages are.

Alternativly installing it to dev dependancies means your project uses the tool version it was meant to, and more importantly new developers are only a single `npm install` away from installing everything they need to start working.

## But i want to access CLI tools
Installing global did have the nice feature that the executables went on your path, this meant after running something like `npm install --global browserify' you could then directly run `browserify main.js` and your computer knew where to find the browserify command. 

But it turns out there is a way to get similiar behaviour and still being able to `npm install --save-dev`. We can simply add the following to your PATH variable

```
./node_modules/.bin
```

Now whenever you try run commands in a project directory it will check to see if you project has any executable dependancies with the correct and try and run those first.


