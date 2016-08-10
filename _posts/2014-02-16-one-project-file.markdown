---
layout: post
title:  "One Project File"
date:   2014-02-16
description: "We have a string and we want to parse it as an object, what to do?"
redirect_from: "/one-project-file"
---

## Single project application
No code for this post, but there is a thought, its not even my thought but one of my colleagues, but it is a good thought which I have found rather contagious so needs spreading.

> "Why have multiple project files in a solution, why not have just one?"

Coming from a very talented developer I was nonplussed at this statement. On the surface this seems ridiculous. This is how it is done. This is standard practice. We need data projects, service projects, utility project, web projects. But once you get over the initial shock reaction and think about it more carefully there is a very good point in here

## Only one executable project
The vast majority of solutions have only one executable project, usually something like a web project, WPF project or console app, regardless of what it is there is usually only one. The rest of the projects are library projects and output dlls. 

Having multiple dlls is only useful if they are going to be sharedd. But most of the time they are not shared, most of the time the dll is only used by a single executable. 

Your MyApp.BusinessService.dll is probably only ever used by MyApp.Web project, and if there is only one thing using it why have it in a separate project? Wouldn't it just be easier to put all the code your web app needs in one web project?

## But what about solution organization?
Projects have folders and namespaces, why have separate projects as well?

## The layers
What about the precious layer model, our web layer should only be talking to our business service layer, and that should only be talking to our data layer. How is this enforced if every layer has access to everything?

In practice the layers tend to get blurry anyway, business layers return data layer objects, web layers skip straight to the data layer to have more fine grain control, the layer thing is an architectural guideline not a rule. I will admit multi-project solutions do assist in enforcing the guideline, but it does make it a pain when you want to disregard the guideline. To pass things up the layers you are forced to make more abstractions write more code and make your application just a bit more bigger and complicated.

Most architectural concerns (not just layers) depend a lot on trust, in both yourself and other developers that they wont mess it up. Single project solutions is no different, if my business service required HttpContextBase then I would be concerned, but not any more concerned than controllers fetching dependencies through static methods. Good developers build good architecture, it does not work the other way around.

## It is just simpler
If your code is a single dll, it is easier to deploy and easier to version. When writing code you don't have to jump hoops to prevent circular dependencies and each class has access to all the code you have written, not just a layered subset. There is just less things that get in your way of you writing code.

## Exceptions to the rule
A blanket statement that all solutions should have only a single project is a bit of an exaggeration, there are legitimate cases where another project is recommended.

* Test projects - These should always be in another project. We do not want all our tests to be deployed with our solution and our tests tend to have dependencies our main application project wont have. There is no good reason to have this in our main project.
* WCF Contracts - With WCF we have a legitimate case where two executable projects want to share a dll, they want to share the service interfaces and data definitions
* Plugin Architectures - If a plugin is a separate dll then it makes perfect sense it is its own project, it probably also wants to reference a shared dll with the application so they can communicate.
* Self Updating Applications - If your application updates itself there is benefits of splitting it out into separate libraries a separate updater application might bootstrap the actual application.

There are probably other exceptions, these are the few that come to mind. But desptie these, the majority of projects tend to be a single applications that do not need to share any dll's. It is these kind of solutions which could be simplified.


## True, but not tried
Ill admit this is still just a compelling thought, I have not done this for any project of any real size. But the next time I get complete control over a project's architecture I would be keen on taking the single project model out for a spin.