#! /usr/bin/env python
#
# Usage: makesfrname.py < gpasm-header-file > forth-file
#

import re, sys

sep = re.compile (';(----- |=====)')
regs = re.compile (';----- Register Files')
bits = re.compile (';----- ((.*) Bits|PORT.)')

reg = re.compile ("(\S+)\s+EQU\s+H'0([0-9A-F]{3})'\s*;?\s*(.*)")
port = re.compile ("(\S+)\s+EQU\s+(\d)\s*;?\s*(.*)")

all_regs = []
all_bits = []

def split (lines):
    before, lines = lines[:1], lines[1:]
    while True:
        if not lines: return before, []
        if sep.match (lines[0]): return before, lines
        before.append (lines[0])
        del lines[0]

def output_regs (lines):
    print()
    print("\\ Registers names")
    print()
    for l in lines:
        x = reg.match (l)
        if x:
            name, addr = x.group(1), x.group(2)
            all_regs.append (name)
            print("0x%s constant %s" % (addr.lower (), name))

def output_bits (lines):
    regname = bits.match(lines[0]).group(1).split()[0]
    if 'n' in regname:
        # CAN constants
        regs = regname.split (', ')
        if regs[-1][:4] == 'and ':
            regs = regs[:-1] + [regs[-1][4:]]
        for r in regs:
            prefix = r.split('n')[0]
            for i in range (8):
                name = r.replace ('n', str(i))
                if name in all_regs:
                    output_bits_for_reg (name, lines, prefix + str(i))
    else:
        output_bits_for_reg (regname, lines)

def output_bits_for_reg (regname, lines, prefix = ''):
    print()
    print("\\ %s bits" % regname)
    print()
    for l in lines:
        x = reg.match (l)
        if not x: x = port.match (l)
        if x:
            name, bit, comment = x.group(1), int(x.group(2)), x.group(3)
            if name[:4] == 'NOT_': name = '/%s' % name[4:]
            if name == 'TO': continue
            if prefix + name in all_bits: continue
            if name[:4] == prefix[:4]: name = name[4:]
            all_bits.append (name)
            d = "%s %d bit %s%s" % (regname, bit, prefix, name)
            if comment:
                print("%-30s    \ %s" % (d, comment))
            else:
                print(d)

def output (lines):
    print('\ This file has been automatically generated')
    print('\ Do not edit by hand')
    while lines:
        if not lines: break
        before, lines = split (lines)
        if before and regs.match (before[0]):
            output_regs (before)
        elif before and bits.match (before[0]):
            output_bits (before)

def main ():
    lines = [l.rstrip('\r\n') for l in sys.stdin]
    output (lines)

if __name__ == '__main__': main ()
