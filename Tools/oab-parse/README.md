OAB-Parse
=========

Purpose
-------
A utility for dumping an organisation's Global Address List (GAL) from its Offline Address Book (OAB).
The tool will parse Outlook's `udetails.oab` file found in `%localappdata%\Microsoft\Outlook\"Offline Address Books"\<UUID>` 
or full .lzx files obtained from Microsoft Exchange, and produce a CSV or JSON file for further processing.

The GAL includes phone numbers, job descriptions, distribution list sizes and additional user data.
This can be of use on Red Team jobs after establishing an initial foothold to identify
additional targets, for onwards attacks or as back-up should the initial access fail.


Installation
------------

* Install the latest version of Python 3 (tested with 3.9 on Windows)
* Change into the root directory of a copy of this repository
* Create a virtual environment to keep the dependencies separate from other apps: `\Python39\python -m venv .venv`
* Activate the virtual environment: `.venv\Scripts\activate` or `source .venv/bin/activate` on *nix
* Install the dependencies: `pip install -r requirements.txt`
* On non-Windows systems, libmspack must also be installed for .lzx file support, e.g. on Kali: `sudo apt-get install libmspack0`

Usage
-----
The tool is CLI based with built in help:
```
(venv) C:\tools\oab-parse>python .\oab-parse.py --help
Usage: oab-parse.py [OPTIONS] INFILE OUTFILE

  Parses Offline Address Books into text output.

  INFILE: Path to the udetails.oab or full .lzx file
  OUTFILE: The file to write to

Options:
  --format [CSV|JSON]  Output file format  [default: CSV]
  --help               Show this message and exit.
```

And displays a progress bar whilst parsing the file

```
(venv) C:\tools\oab-parse>python oab-parse.py --format=CSV C:\Jobs\ABCD\udetails.oab C:\Jobs\ABCD\gal.csv
Parsing 9570 records...
  [########################------------]   68%  00:00:02

```

The resulting CSV file can be imported into Excel for filtering & searching.
