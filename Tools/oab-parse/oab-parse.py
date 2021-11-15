# Parses Offline Address Book files (i.e. %localappdata%\Microsoft\Outlook\"Offline Address Books"\<UUID>\udetails.oab) according to
# https://docs.microsoft.com/en-us/openspecs/exchange_server_protocols/ms-oxoab/
import csv
import json
from base64 import b64encode
from uuid import UUID

import bitstream
import click
from click import progressbar
from numpy import *
from bitstream import *

# Attributes found in the header record
RG_HDR_ATTS = {
    # rgHdrAtts
    "6800": "OfflineAddressBookName",
    "6804": "OfflineAddressBookDistinguishedName",
    "6801": "OfflineAddressBookSequence",
    "6802": "OfflineAddressBookContainerGuid",
    "8C98": "AddressBookHierarchicalRootDepartment"
}

# Attributes found in the Address Book object records
TG_OAB_ATTS = {
    # tgOabAtts
    "3003": "EmailAddress",
    "39FE": "SmtpAddress",
    "3001": "DisplayName",
    "8C92": "AddressBookPhoneticDisplayName",
    "3A00": "Account",
    "3A11": "Surname",
    "8C8F": "AddressBookPhoneticSurname",
    "3A06": "GivenName",
    "8C8E": "AddressBookPhoneticGivenName",
    "800F": "AddressBookProxyAddresses",
    "3A19": "OfficeLocation",
    "3900": "DisplayType",
    "0FFE": "ObjectType",
    "3A40": "SendRichInfo",
    "3A08": "BusinessTelephoneNumber",
    "3A0A": "Initials",
    "3A29": "StreetAddress",
    "3A27": "Locality",
    "3A28": "StateOrProvince",
    "3A2A": "PostalCode",
    "3A26": "Country",
    "3A17": "Title",
    "3A16": "CompanyName",
    "8C91": "AddressBookPhoneticCompanyName",
    "3A30": "Assistant",
    "3A18": "DepartmentName",
    "8C90": "AddressBookPhoneticDepartmentName",
    "8011": "AddressBookTargetAddress",
    "3A09": "HomeTelephoneNumber",
    "3A1B": "Business2TelephoneNumbers",
    "3A2F": "Home2TelephoneNumbers",
    "3A23": "PrimaryFaxNumber",
    "3A1C": "MobileTelephoneNumber",
    "3A2E": "AssistantTelephoneNumber",
    "3A21": "PagerTelephoneNumber",
    "3004": "Comment",
    "3A22": "UserCertificate",
    "3A70": "UserX509Certificate",
    "8C6A": "AddressBookX509Certificate",
    "8006": "AddressBookHomeMessageDatabase",
    "39FF": "AddressBookDisplayNamePrintable",
    "3905": "DisplayTypeEx",
    "8CA0": "AddressBookSeniorityIndex",
    "8CDD": "AddressBookHierarchicalIsHierarchicalGroup",
    "8C6D": "AddressBookObjectGuid",
    "8CAC": "AddressBookSenderHintTranslations",
    "806A": "AddressBookDeliveryContentLength",
    "8CB5": "AddressBookModerationEnabled",
    "8CE2": "AddressBookDistributionListMemberCount",
    "8CE3": "AddressBookDistributionListExternalMemberCount",
    "8009": "AddressBookMember",
    "8008": "AddressBookIsMemberOfDistributionList",
    "6805": "OfflineAddressBookTruncatedProperties",
}

TRUNCATED_PROPERTIES = {
    "8C9E": "ThumbnailPhoto",
    "8CC2": "SpokenName",
    "8CD8": "AddressBookAuthorizedSenders",
    "8CD9": "AddressBookUnauthorizedSenders",
    "8073": "AddressBookDistributionListMemberSubmitAccepted",
    "8CDA": "AddressBookDistributionListMemberSubmitRejected",
    "8CDB": "AddressBookDistributionListRejectMessagesFromDLMembers"
}

FIELD_NAMES = {**RG_HDR_ATTS, **TG_OAB_ATTS, **TRUNCATED_PROPERTIES}


def get_field_name(data_purpose):
    hex_data_purpose = '{:04X}'.format(data_purpose)
    if hex_data_purpose in FIELD_NAMES:
        return FIELD_NAMES[hex_data_purpose]
    else:
        # Some Tags are not even published in the master MS-OXPROPS spec
        return f"Unknown{hex_data_purpose}"


class PtypInteger32:
    pass


class PtypBoolean:
    pass


# Spec says String supports UTF-8, but String 8 is ambiguous
class PtypString:
    def __init__(self, charset):
        self.charset = charset


class PtypBinary:
    pass


def read_PtypInteger32_factory(instance):
    def read_PtypInteger32(stream, n=None):
        if n is None or n == 1:
            first_byte_as_int = int.from_bytes(stream.read(bytes, 1), byteorder='little')
            if first_byte_as_int <= 127:
                return first_byte_as_int
            else:
                bytecount = first_byte_as_int - 128
                int_bytes = int.from_bytes(stream.read(bytes, bytecount), byteorder='little')
                return int_bytes
        else:
            return [read_PtypInteger32(stream) for _ in range(n)]

    return read_PtypInteger32


def read_PtypBoolean_factory(instance):
    def read_PtypBoolean(stream, n=None):
        if n is None or n == 1:
            return bool(stream.read(bytes, 1) == b'\x01')
        else:
            return [read_PtypBoolean(stream) for _ in range(n)]

    return read_PtypBoolean


def read_PtypString_factory(instance):
    charset = instance.charset

    def read_PtypString(stream, n=None):
        if n is None or n == 1:
            result = ""
            while True:
                readbyte = bytes(stream.read(bytes, 1))

                if readbyte == b'\x00':
                    return result
                else:
                    result += readbyte.decode(charset)
        else:
            return [read_PtypString(stream) for _ in range(n)]

    return read_PtypString


def read_PtypBinary_factory(instance):
    def read_PtypBinary(stream, n=None):
        if n is None or n == 1:
            bytes_to_read = stream.read(PtypInteger32())
            return stream.read(bytes, bytes_to_read)
        else:
            return [read_PtypBinary(stream) for _ in range(n)]

    return read_PtypBinary


bitstream.register(PtypInteger32, reader=read_PtypInteger32_factory)
bitstream.register(PtypBoolean, reader=read_PtypBoolean_factory)
bitstream.register(PtypString, reader=read_PtypString_factory)
bitstream.register(PtypBinary, reader=read_PtypBinary_factory)


def parse_OAB_PROP_TABLE(input):
    """
    Parse the property table.

    :return: A tuple of: The number of bytes in the record bit field AND the list of all fields
    """
    field_list = []

    table_entries = input.read(uint32).newbyteorder()  # AKA cAttrs
    for table_entry in range(table_entries):
        # Parse the OAB_PROP_REC
        data_type_raw = input.read(uint16).newbyteorder()
        data_purpose = input.read(uint16).newbyteorder()
        padding = input.read(uint32).newbyteorder()

        is_array = bool(data_type_raw & 0x1000)

        data_type_raw = data_type_raw & 0xEFFF

        if data_type_raw == 0x03:
            data_type = PtypInteger32()
        elif data_type_raw == 0x0B:
            data_type = PtypBoolean()
        elif data_type_raw == 0x0D:
            data_type = object  # No guidance in spec on how to parse objects
        elif data_type_raw == 0x0102:
            data_type = PtypBinary()
        elif data_type_raw == 0x1E:
            data_type = PtypString("UTF-8")
        elif data_type_raw == 0x1F:
            data_type = PtypString("latin-1")
        else:
            print(f"Unexpected data type: {data_type_raw}")
            data_type = None

        field_list.append((data_type, is_array, get_field_name(data_purpose)))

    bytes_in_record_bits = int(ceil(table_entries / 8))

    return bytes_in_record_bits, field_list


def parse_OAB_V4_REC(input, bit_size, field_defs):
    result = {}

    record_size = input.read(uint32).newbyteorder() * 8
    record = input.read(BitStream, record_size - 32)

    record_fields_in_use = (record.read(bool, bit_size * 8))

    for i, field in enumerate(field_defs):
        if record_fields_in_use[i]:
            if field[1]:  # Is Array
                array_size = record.read(PtypInteger32())
            else:
                array_size = 1

            value = record.read(field[0], array_size)
            result[field[2]] = value

    return result


def parse_Uncompressed_OAB_v4_Full_Details(input):
    # OAB_HDR
    format = input.read(uint32).newbyteorder()
    if format != 32:
        exit("Not an OAB version 4 Full Details file")

    checksum = input.read(uint32).newbyteorder()
    records = input.read(uint32).newbyteorder()

    print(f"Parsing {records} records...")

    # OAB_META_DATA
    metadata_size = input.read(uint32).newbyteorder()

    # PROP_TABLE x 2
    global_bit_size, global_fields_defs = parse_OAB_PROP_TABLE(input)
    record_bit_size, record_fields_defs = parse_OAB_PROP_TABLE(input)

    # Parse OAB Header record
    result = {"OABHeader": parse_OAB_V4_REC(input, global_bit_size, global_fields_defs),
              "Records": []}

    # Parse Address Book object records
    with progressbar(range(records)) as bar:
        for i in bar:
            result["Records"].append(parse_OAB_V4_REC(input, record_bit_size, record_fields_defs))

    return result


def post_process(data):
    """
    Post process the parsed data in place to improve readability.

    :param data: The parsed data.
    """
    for record in data["Records"]:
        for name, value in record.items():
            if type(value) == list:
                newlist = []
                for entry in value:
                    newlist.append(post_process_pair(name, entry))
                record[name] = newlist
            else:
                record[name] = post_process_pair(name, value)


def post_process_pair(name, value):
    """
    Post process a given name/pair value to improve readabilty.

    :param name: The record name (i.e. value in FIELD_NAMES)
    :param value: The parsed value
    :return: The cleaned up value
    """
    if name == "AddressBookObjectGuid" and \
            type(value) == bytes and \
            len(value) == 16:
        value = str(UUID(bytes=value))

    if type(value) == bytes:
        # Base64 encode binary content
        value = b64encode(value).decode("ASCII")

    if name == "OfflineAddressBookTruncatedProperties":
        value = get_field_name(value >> 16)

    if name == "ObjectType":
        if value == 3:
            value = "Folder"
        elif value == 6:
            value = "User"
        elif value == 8:
            value = "Distribution List"

    return value


@click.command()
@click.argument('infile', type=click.File('rb'))
@click.argument('outfile', type=click.File('w', encoding="UTF-8"))
@click.option('--format', default="CSV", type=click.Choice(['CSV', 'JSON'], case_sensitive=False), show_default=True,
              help="Output file format")
def main(infile, outfile, format):
    """
    Parses Offline Address Books into text output.

    \b
    INFILE: Path to the udetails.oab file
    OUTFILE: The file to write to

    \f
    :param infile: The udetails.oab file
    :param outfile: The file to write to
    :param format: Choice of output format, e.g. CSV
    """
    input = BitStream(infile.read())
    data = parse_Uncompressed_OAB_v4_Full_Details(input)

    post_process(data)

    if format == "CSV":
        fieldnames = []
        for row in data["Records"]:
            for field in row.keys():
                if field not in fieldnames:
                    fieldnames.append(field)

        writer = csv.DictWriter(outfile, fieldnames=fieldnames, lineterminator='\n')

        writer.writeheader()
        for row in data["Records"]:
            writer.writerow(row)
    elif format == "JSON":
        json.dump(data, outfile)


if __name__ == '__main__':
    main()
