#! /usr/bin/env python
#
# Copyright (c) 2004-2006 Samuel Tardieu <sam@rfc1149.net>
#
# This file allows the loading of an hex file onto a PIC 18Fxx8 device
# using a serial monitor.
#
# Usage: monitor.py serialport hexfile
#
# Example for the first communication port:
#   - FreeBSD:     monitor.py /dev/cuaa0 hexfile
#   - Linux:       monitor.py /dev/ttyS0 hexfile
#   - DOS/Windows: monitor.py com1: hexfile
#

import optparse, socket, serial, snooper, sys

flash = [0xff] * 65536

BadChecksum = 'BadChecksum'
BadLength = 'BadLength'
PermissionDenied = 'PermissionDenied'
TransmissionWithServerFailed = 'TransmissionWithServerFailed'

def get (list, n):
    r = list[0]
    del list[0]
    if n == 1: return r
    return (get (list, n-1) << 8) | r

class Config:

    params = [('magic', 2), ('version', 2), ('ledlatch', 2),
              ('ledconf', 1), ('buttonport', 2), ('buttonconf', 1),
              ('spbrg', 1), ('spbrgconf', 1), ('confbits', 1),
              ('frequency', 1), ('canaddr', 2), ('waitdelay', 2)]

    unknown_parameter = 'UNKNOWN_PARAMETER'

    def __init__ (self, values):
        self.values = values

    def get_value (self, name):
        l = self.values[:]
        for n, s in Config.params:
            v = get (l, s)
            if n == name: return v
        raise Config.unknown_parameter

    def set_value (self, name, value):
        c = 0
        for n, s in Config.params:
            if n == name:
                before = self.values[:c]
                after = self.values[c+s:]
                if s == 1:
                    self.values = before + [value] + after
                    return
                else:
                    self.values = before + [value % 256, value / 256] + after
                    return
            c += s
        raise Config.unknown_parameter

    def print_config (self):
        for i in [x for x, y in Config.params]:
            print "%12s = %04x" % (i, self.get_value (i))

class Snooper:

    def __init__ (self, fd):
        self.fd = fd

    def write (self, data):
        print ">>> %s" % data
        self.fd.write (data)
        self.fd.flush ()

    def read (self, l = None):
        if l is None:
	  data = self.fd.read ()
	else:
	  data = self.fd.read (l)
        print "<<< %s" % data
        return data

    def close (self):
        self.fd.close ()

    def flush (self):
        self.fd.flush ()
        
def read_address (fd, a):
    writefd (fd, "R%06x" % a)
    return int (fd.read(5)[1:5], 16)

def read_line (fd, a):
    return [read_address (fd, x) for x in range (a, a+64)]

def print_config (fd):
    line = read_line (fd, 0x1FC0)
    conf = Config (line)
    conf.print_config ()

def check_checksum (l):
    """Raise an exception BadChecksum if the line is malformed."""
    sum = 0
    l = l[1:]
    for i in range (0, len (l), 2):
        sum = (sum + int (l[i:i+2], 16)) % 256
    if sum != 0: raise BadChecksum

def handle_hex_line (l):
    """Decode hex line and fill up memory."""
    check_checksum (l)
    if l[7:9] != '00': return    # Not a code line
    addr = int (l[3:7], 16)
    data = l[9:-2]
    if len(data) != 2 * int (l[1:3], 16): raise BadLength
    for i in range (0, len (data), 4):
        byte1 = int (data[i:i+2], 16)
        byte2 = int (data[i+2:i+4], 16)
        flash [addr + i/2] = byte1
        flash [addr + i/2 + 1] = byte2

def makeup_lines (options):
    """Return a list of lines to program (address, data)."""
    lines = []
    for a in range (0, len (flash), 64):
        line = flash [a:a+64]
        if line != [0xff] * 64:
            if a < 0x200 and not options.force:
                raise PermissionDenied, 'Set force flag to True'
            lines.append ((a, line))
    return lines

def writefd (fd, data):
    if type (fd) == type (''):
        fd = open (fd, "wb")
        fd.write (data)
        fd.close ()
    else:
        fd.write (data)
        if data == 'TT':
            wait_for (fd, '!')
            wait_for (fd, '!')
        else:
            wait_for (fd, data)

def wait_for (fd, s):
    for c in s:
        while c != fd.read (): pass

def wait_for_prompt (fd):
    wait_for (fd, "ok>")

def sync_device (fd):
    writefd (fd, "TT")

def program_device (fd, options):
    """Program the device."""
    print "Switching to bootloader mode"
    if options.sync: sync_device (fd)
    for addr, data in makeup_lines(options):
        print "Programming flash starting at %06X" % addr
        writefd (fd, "W%06X" % addr)
        for i in data: writefd (fd, "%02X" % i)
        wait_for_prompt (fd)

def load_hex_file (file):
    """Load a hex file in memory."""
    print "Loading %s" % file
    for l in open (file, 'r'):
        l = l.rstrip('\r\n')
        if l[:1] == ':': handle_hex_line (l)

def open_port (options):
    print "Opening %s at %s bps" % (options.port, options.speed)
    fd = serial.Serial (options.port, options.speed)
    if options.snoop:
        print "Activating snooper"
        fd = Snooper (fd)
    if options.sync: sync_device (fd)
    if options.can:
        print "Connecting to remote CAN device %03x" % int (options.can, 16)
        writefd (fd, ":%03x" % int (options.can, 16))
        if options.sync: sync_device (fd)
    return fd

def close_port (fd, options):
    if options.can:
        print "Disconnecting from remote CAN deviec"
        writefd (fd, chr (27))

def check_argv (args, n):
    if len (args) != n:
        print "Error: this mode requires a different number of arguments " \
              "(%d instead of %d given)" % (n, len (args))
        sys.exit (1)

def action_dump (options, args):
    check_argv (args, 0)
    fd = open_port (options)
    try:
        c = Config (read_line (fd, 0x1FC0))
        c.print_config ()
    finally:
        close_port (fd, options)

def action_program (options, args):
    check_argv (args, 1)
    fd = open_port (options)
    try:
        load_hex_file (args[0])
        program_device (fd, options)
    finally:
        if options.execute:
            writefd(fd, "X002000")
        close_port (fd, options)

def handle_client (client):
    ACK="ok"
    while 1:
        l = client.recv(1024)
        if not l:raise TransmissionWithServerFailed 	 
        if l=="eof": break
        while l[-1:] in ['\r', '\n']: 
            l = l[:-1]
        if l[:1] == ':': 
            handle_hex_line (l)
        client.send(ACK)

def action_client (options, args):
    check_argv (args, 1)
    try:
        s = socket.socket(socket.AF_INET,socket.SOCK_STREAM)
        host, port = options.server_addr, options.server_port
        if host == 'auto': host = 'localhost'
        s.connect((host, port))
        print "Connected to", addr
        print "Sending file %s" % args[0]	
        for l in open (args[0],'r').readlines():
            s.send(l)
            data = ''
            while len(data) < 2:
                data += s.recv(2 - len(data))
            if data != "ok":
                raise TransmissionWithServerFailed 	
        s.send("eof")
        while 1:
            data = s.recv(1024)
            if not data:raise TransmissionWithServerFailed 	
            if data == "bye": break
            print data
    finally:
        s.close()
		
def action_server (options, args):
    check_argv (args, 0)
    try:
        s = socket.socket(socket.AF_INET,socket.SOCK_STREAM)
        port = options.server_port
        host=options.server_addr
        if host == 'auto':
            host= ''
        addr = (host, port)	
        s.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1)
        s.bind(addr)
        s.listen(1)
        print "Listening on ", addr

        while 1:
            client,addr = s.accept()
            print "Connection from ", addr
            handle_client (client)
            client.send("Hex file loaded")
            fd = open_port (options)
            program_device (fd, options)
            close_port (fd,options)
            client.send("Device programmed")
            client.send("bye")
            client.close()
    finally:
            s.close()
			
def main ():
    usage = '%prog [options] [hexfile]'
    description = """Communicate with the embedded bootloader.

You have to choose one mode amongst program, dump, client or server.
Each mode has its own option requirements."""
    parser = optparse.OptionParser (usage = usage, description = description)
    parser.add_option ('--server',
                       action = 'store_const',
                       const = action_server,
                       dest = 'action',
                       default = None,
                       help = 'activate server mode')
    parser.add_option ('--client',
                       action = 'store_const',
                       const = action_client,
                       dest = 'action',
                       help = 'activate client mode')
    parser.add_option ('--server-addr', metavar = 'ADDR', dest = 'server_addr',
                       default = 'auto',
                       help = 'server address')
    parser.add_option ('--server-port', metavar = 'PORT', type = 'int',
                       dest = 'server_port',
                       help = 'server port')
    parser.add_option ('-c', '--can', action = 'store_true',
                       default = False,
                       help = 'program through CAN to serial interface')
    parser.add_option ('-D', '--dump',
                       action = 'store_const',
                       const = action_dump,
                       dest = 'action',
                       help = 'dump configuration')
    parser.add_option ('--force', action = 'store_true', default = 'False',
                       help = 'allow programming of low bytes (dangerous!)')
    parser.add_option ('--no-sync', action = 'store_false', default = 'True',
                       dest = 'sync',
                       help = 'skip synchronization phase')
    parser.add_option ('-p', '--port', default = '/dev/ttyS0',
                       help = 'communication port to use [/dev/ttyS0]')
    parser.add_option ('-P', '--program',
                       action = 'store_const',
                       const = action_program,
                       dest = 'action',
                       help = 'programming mode')
    parser.add_option ('--snoop', action = 'store_true', default = False,
                       help = 'snoop ongoing traffic while programming')
    parser.add_option ('-s', '--speed', type = 'int',
                       help = 'speed to use for programming [115200]',
                       default = 115200)
    parser.add_option('-X', '--execute', action = 'store_true', default = False,
                       help = 'launch the program at address 0x2000')
    opts, args = parser.parse_args ()
    if opts.action is None:
        parser.print_help ()
        sys.exit (1)
    opts.action (opts, args)

if __name__ == '__main__':
    main ()

