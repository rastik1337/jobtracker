# J*bTracker

## Description

A tool to track time spent working on different projects.

The idea is that a user creates projects to group time records together. Everytime
a user wants to track his work on a project, he inserts a new record and optionally
labels it, so it can be later identified what exactly he has worked on in the context
of the project.

For example if I work as a web developer on a project, I might want to distinguish
between the time I've spent putting the website together and the time I've spent
reviewing the code with my colleague. Therefore I will label each time record with
either `CODE` or `CODE_REVIEW` tag. Then, if I later decide that I want to keep
the track of the meeting time too, I can just simply add a `MEETING` tag to the
next record.

## Tech stack and target platform

- Avalonia UI
- LiteDB
- Should run on both Windows and Linux (Ubuntu Gnome)

## Features

- CRUD operations on time records, projects and labels
- Statistics of all records with the option to select specific projects or labels
  within them and during a specified time period

## Links

[Why j*b?](https://en.wiktionary.org/wiki/j*b)
