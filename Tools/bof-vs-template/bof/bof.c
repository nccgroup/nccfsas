#include <windows.h>
#include <stdio.h>
#include <tlhelp32.h>
#include "beacon.h"

void demo(char* args, int length) {
	datap  parser;
	char* str_arg;
	int    num_arg;

	BeaconDataParse(&parser, args, length);
	str_arg = BeaconDataExtract(&parser, NULL);
	num_arg = BeaconDataInt(&parser);

	BeaconPrintf(CALLBACK_OUTPUT, "Message is %s with %d arg", str_arg, num_arg);
}