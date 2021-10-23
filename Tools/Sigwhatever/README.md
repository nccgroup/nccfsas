# Sigwhatever

For automated exploitation of netntlm hash capture via image tags in emails signatures. This targets Outlook specifically and will insert a 1x1px image into an existing signature block, or create a new signature as required. A listener is then started to capture authentication attempts that happen as a result of sent emails being viewed by other users.


The tool borrows code from the Seatbelt and Inveigh projects - features are:

* Queries the firewall for suitable ports to listen on (Uses some seatbelt code)
* Cross references HttpQueryServiceConfiguration for any usable URL ACLs
* TCP/HTTP server and hash capture (Uses Inveigh code)
* Signature Detection (to modify the appropriate registry settings and signatures)
* Modification of Signature
* Feature to send mail to specific group (e.g. domain admins)
* Option to create encrypted logs on disk
* Cleanup (Reverts changes in signature settings and existing signature)



---

## TLDR

Run: ```execute-assembly sigwhatever.exe AUTO```


Then when you're finished, run: ```execute-assembly sigwhatever.exe CLEANUP```


*Bear in mind that even running jobkill on the .net job does not seem to kill the spawned process.*

---


## Usage

Usage: `SigWhatever.exe OPERATION [OPTIONS]`


### Options
```
  -p, --port=VALUE           TCP Port.
  -l, --log=VALUE            Log file path.
  -g, --group=VALUE          Target Active Directory group.
  -f, --force                Force HTTP server start.
      --ba, --backdoor-all   Backdoor all signatures.
  -c, --challenge=VALUE      NTLM Challenge (in hex).
  -u, --url-prefix=VALUE     URL Prefix. e.g. /MDEServer/test
  -h, --help                 Show this message and exit.
  ```


### Operation

* `AUTO` - Just do everything for me - backdoor the signature and start the listener on this box.
  * `SigWhatever.exe AUTO`
* `CHECKTRUST` - Check whether the trust zone settings - if the domain isn't in there then this probably won't work
  * `SigWhatever.exe CHECKTRUST`
* `CHECKFW` - Check whether the host based firewall is on and whether there's an exception for the chosen port
  * `SigWhatever.exe CHECKFW -p <port>`
* `SIGNATURE` - hijack the current user's signature, or add a new one via registry changes
   * `SigWhatever.exe SIGNATURE [-p <port>] [-l <logfile>] -u <url prefix> [--backdoor-all] [--force]`
* `SIGNOLISTEN` - hijack the current user's signature, or add a new one via registry changes and **don't** start a listener.
  * `SigWhatever.exe SIGNOLISTEN -s <server> -p <port> -l <logfile> [--backdoor-all]`
* `CLEANUP` - Remove any modifications to the registry or htm signature files
  * `SigWhatever.exe CLEANUP`
* `EMAILADMINS` - Enumerate email addresses from an AD group and send them a 'blank' email with the payload.
  * `SigWhatever.exe EMAILADMINS -g <Active Directory group> -p <port> [-l <logfile>] [--force]`
* `LISTENONLY` - Just start the listener - make sure it's on the same port
  * `SigWhatever.exe LISTENONLY -p <port> [-l <logfile>]`
* `SHOWACLS` - List all URL Reservation ACLs with User, Everyone or Authenticated Users permissions.
  * `SigWhatever.exe SHOWACLS`


**NOTE: With the signature option, if --backdoor-all is not specified then the tool will attempt to get the current signature from Outlook - this may cause a popup for the user if their AV is out of date.**
  
Authors: David Cash, Rich Warren, Julian Storr 
