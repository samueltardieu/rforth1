TESTCASES = tests/test-suite.cmp tests/balise.cmp tests/sensors.cmp \
            tests/test-bitops.cmp tests/pwm.cmp \
            tests/test-can.cmp tests/test-plusminus.cmp tests/spi-pic.cmp \
            tests/colortest.cmp tests/interrupts.cmp tests/increment.cmp \
            tests/lshift.cmp tests/interrupts2.cmp tests/address_of.cmp \
            tests/cfor.cmp tests/switchw.cmp tests/retlw.cmp \
            tests/charinterp.cmp tests/duptow.cmp tests/cfetchdup.cmp \
            tests/cfetchdrop.cmp tests/wnoop.cmp tests/doublezero.cmp \
            tests/bitmask.cmp tests/addzero.cmp tests/staticminus.cmp \
            tests/staticmult.cmp tests/oneplusstore.cmp \
            tests/cplusstore.cmp tests/recurse.cmp tests/store.cmp \
            tests/eeprom.cmp tests/values.cmp tests/commas.cmp \
            tests/prefixasm.cmp tests/inline.cmp

ITESTCASES = ${TESTCASES:.cmp=.icmp}

COMPILER = rforth.py

PREDEFINED = lib/core.fs lib/sfrnames.fs lib/primitives.fs lib/arithmetic.fs \
             lib/tables.fs lib/strings.fs lib/math.fs lib/memory.fs \
             lib/canlib.fs

STARTADDR ?= 0x2000
PORT ?= /dev/ttyS0
SPEED ?= 115200
FLAGS = ${OPTS} --start ${STARTADDR}

PYTHON ?= python

TEXI2PDF ?= texi2pdf
TEXI2HTML ?= texi2html

tests: ${TESTCASES} ${ITESTCASES}

never::

update-tests: ${TESTCASES:.cmp=.newref}

doc: doc/rforth1.html doc/rforth1.pdf

doc/rforth1.html: doc/rforth1.texi
	cd doc && ${TEXI2HTML} rforth1.texi

doc/rforth1.pdf: doc/rforth1.texi
	cd doc && ${TEXI2PDF} rforth1.texi

clean:
	${RM} *.asm tests/*.asm examples/*.asm examples/engines/*.asm
	${RM} *.hex tests/*.hex examples/*.hex examples/engines/*.hex
	${RM} *.lst tests/*.lst examples/*.lst examples/engines/*.lst
	${RM} *.map tests/*.map examples/*.map examples/engines/*.map
	${RM} *.cod tests/*.cod examples/*.cod examples/engines/*.cod
	${RM} doc/rforth1.{aux,cp,fn,ky,log,pg,toc,tp,vr}

%.asm %.hex %.lst %.map %.cod: %.fs ${COMPILER} ${PREDEFINED}
	${PYTHON} ${COMPILER} ${FLAGS} $<

%.cmp: %.ref never
	${RM} ${@:.cmp=.asm}
	${MAKE} ${@:.cmp=.asm} OPTS="--no-comments" 2> /dev/null
	diff -u ${@:.cmp=.ref} ${@:.cmp=.asm}

%.icmp: %.iref never
	${RM} ${@:.icmp=.asm}
	${MAKE} ${@:.icmp=.asm} OPTS="--no-comments -a" 2> /dev/null
	diff -u ${@:.icmp=.iref} ${@:.icmp=.asm}

%.newref: never
	${RM} ${@:.newref=.asm}
	${MAKE} ${@:.newref=.asm} OPTS="--no-comments" 2> /dev/null
	cp -p ${@:.newref=.asm} ${@:.newref=.ref}
	${RM} ${@:.newref=.asm}
	${MAKE} ${@:.newref=.asm} OPTS="--no-comments -a" 2> /dev/null
	cp -p ${@:.newref=.asm} ${@:.newref=.iref}

%.load: %.hex
	${PYTHON} utils/monitor.py --program --port=${PORT} --speed=${SPEED} $<

tests/interrupts.asm: tests/interrupts.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts.fs

tests/interrupts2.asm: tests/interrupts2.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts2.fs
