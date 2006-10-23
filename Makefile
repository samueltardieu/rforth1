TESTCASES = tests/test-suite.cmp tests/balise.cmp tests/sensors.cmp \
            tests/test-bitops.cmp tests/pwm.cmp \
            tests/test-can.cmp tests/test-plusminus.cmp tests/spi-pic.cmp \
            tests/colortest.cmp tests/interrupts.cmp tests/increment.cmp \
            tests/lshift.cmp tests/interrupts2.cmp tests/address_of.cmp

COMPILER = rforth.py

PREDEFINED = lib/core.fs lib/sfrnames.fs lib/primitives.fs lib/arithmetic.fs \
             lib/tables.fs lib/strings.fs lib/math.fs lib/memory.fs \
             lib/canlib.fs

STARTADDR ?= 0x2000
PORT ?= /dev/ttyS0
SPEED ?= 115200
FLAGS = ${OPTS} --start ${STARTADDR}

PYTHON ?= python

tests: never
	${MAKE} ${TESTCASES} OPTS="--no-comments"

never::

update-tests: never
	${MAKE} ${TESTCASES:.cmp=.newref} OPTS="--no-comments"

clean:
	${RM} *.asm tests/*.asm examples/*.asm examples/engines/*.asm
	${RM} *.hex tests/*.hex examples/*.hex examples/engines/*.hex
	${RM} *.lst tests/*.lst examples/*.lst examples/engines/*.lst
	${RM} *.map tests/*.map examples/*.map examples/engines/*.map
	${RM} *.cod tests/*.cod examples/*.cod examples/engines/*.cod

%.asm %.hex %.lst %.map %.cod: %.fs ${COMPILER} ${PREDEFINED}
	${PYTHON} ${COMPILER} ${FLAGS} $<

%.cmp: %.asm %.ref
	diff -u ${@:.cmp=.ref} ${@:.cmp=.asm}

%.newref: %.asm
	cp -p ${@:.newref=.asm} ${@:.newref=.ref}

%.load: %.hex
	${PYTHON} utils/monitor.py --program --port=${PORT} --speed=${SPEED} $<

tests/interrupts.asm: tests/interrupts.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts.fs

tests/interrupts2.asm: tests/interrupts2.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts2.fs
