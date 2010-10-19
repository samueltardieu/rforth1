#! /usr/bin/env python
#

import re, sys

_exr = re.compile ('(\s*)(\S+)\s(.*\S)(.*)')

def main ():
    in_code = False
    for l in sys.stdin.readlines ():
        while l[-1:] in ['\r', '\n']: l = l[:-1]
        if l in ['prefix', 'postfix']: continue
        if not in_code and l[:5] == 'code ':
            in_code = True
        elif in_code and l[:5] == ';code':
            in_code = False
        elif in_code and l[:6:] != 'label ':
            s = re.split ('\s+\\\\', l, 1)
            code = s[0]
            if len (s) == 2: comment = '   \\' + s[1]
            else: comment = ''
            x = _exr.match (code)
            if x:
                l = "%s%s %s%s%s" % (x.group (1), x.group (3), x.group (2),
                                     x.group (4), comment)
        print(l)

if __name__ == '__main__': main ()
            
