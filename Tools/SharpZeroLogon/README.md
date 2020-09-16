# SharpZeroLogon

This is an exploit for CVE-2020-1472, a.k.a. Zerologon. This tool exploits a cryptographic vulnerability in Netlogon to achieve authentication bypass. Ultimately, this allows for an attacker to reset the machine account of a target Domain Controller, leading to Domain Admin compromise.

The vulnerability was discovered by Tom Tervoort of Secura BV, and was addressesd by Microsoft on August 11th 2020. You can read more about the vulnerability in [their excellent whitepaper](https://www.secura.com/blog/zero-logon).

Although other exploits exist, this tool is aimed at working with Cobalt Strike's `execute-assembly` functionality. Therefore it is written in C# using functions from `netapi32.dll`. The nice thing here is that due to the structures being zero by default, we do not need to mess with any packets and can use the APIs provided by Microsoft cleanly (relatively ;).

# Running

## Checking if the server is vulnerable

To run the exploit, from a domain joined machine (see method below for non domain-joined) run the `SharpZeroLogon.exe` binary, providing the FQDN of the Domain Controller.

Running it with only one argument will test whether the target Domain Controller is vulnerable to CVE-2020-1472.

In the following example, the FQDN of the Domain Controller is `win-dc01.vulncorp.local`:

```
execute-assembly SharpZeroLogon.exe win-dc01.vulncorp.local
```

If the Domain Controller is vulnerable, you will receive a message indicating it was Successful, otherwise the server has likely been patched and is not vulnerable.

## Resetting the machine account password

Firstly, it is **very important** to note that resetting the Domain Controller machine account password in this manner **will likely break functionality**. You should not do this on a production system without the system owner understanding that there may be an impact. Of course once you have reset the password, you can then carry out a dcsync (using `pth` with the machine account), and subsequently reset the password using a Domain Admin account via an [official method](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.management/reset-computermachinepassword?view=powershell-5.1). However, it is important to understand the potential impact in a lab environment before running it blindly.

To reset the machine account, run the following command (specifying your DC FQDN):

```
execute-assembly SharpZeroLogon.exe win-dc01.vulncorp.local -reset
```

Once the machine account password is reset, you can use `pth` to impersonate the machine account and perform a DCSync.

## Testing from a non Domain-joined machine

By default the `netapi32.dll` functions use RPC over SMB named pipe (ncacn_np), which requires an authenticated session (i.e. a domain-joined client). Benjamin Delpy (@gentilkiwi) found a way round this by patching `logoncli.dll` with a single byte patch that forces RPC over TCP/IP (ncacn_ip_tcp) instead, which he has implemented in Mimikatz. This patch allows the exploit to work from a non domain-joined client as well.

To run the exploit from a non domain-joined context, use the `-patch` flag, which will force the client to use RPC over TCP/IP.

For example:

```
execute-assembly SharpZeroLogon.exe win-dc01.vulncorp.local -patch
```

Note that the patch is designed to work on x64 clients only.

## Detection

* A [sample PCAP](https://github.com/sbousseaden/PCAP-ATTACK/blob/master/Lateral%20Movement/CVE-2020-1472_Zerologon_RPC_NetLogon_NullChallenge_SecChan_6_from_nonDC_to_DC.pcapng) of a Zerologon attempt is provided by @sbousseaden.
* Successful exploitation resulting in a password change will show as event ID 4742, Password last set change, performed by Anonymous Logon.
* Adam Swan of SOC Prime provides a [Sigma rule](https://socprime.com/blog/zerologon-attack-detection-cve-2020-1472/) which can be used to detect Zerologon attempts.
* For detecting default `pth` usage in Cobalt Strike, look for command lines containing `/c echo` and `\\.\pipe\` together. Default Cobalt Strike also uses 11 hex characters for the echo argument, and 6 hex characters for the pipe name. This requires manually patching and is not easily configurable by the operator.
* To detect DCSync usage, look for event ID 4662 containing the GUID `{1131f6ad-9c07-11d1-f79f-00c04fc2dcd2}`, which is the `DS-Replication-Get-Changes-All` extended right required for replication. Any replication from a non Domain Controller is suspicious. @James_inthe_box also provides [this Snort](https://gist.github.com/silence-is-best/25ae0929c277642e86ecf592598a3254) rule.

# References
* https://portal.msrc.microsoft.com/en-US/security-guidance/advisory/CVE-2020-1472
* https://www.secura.com/blog/zero-logon
* https://github.com/dirkjanm/CVE-2020-1472
* https://twitter.com/gentilkiwi/status/1305659499991183361
* https://twitter.com/gentilkiwi/status/1305975783781994498
* https://github.com/gentilkiwi/mimikatz/commit/880c15994c4955d232f83cd2f73e5b6b1de165e7
