TESTFILES = $(wildcard tests/*.fs)

TESTCASES = ${TESTFILES:.fs=.cmp}
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

tests-gputils::
	wget -O - http://downloads.sourceforge.net/sourceforge/gputils/gputils-1.5.0.tar.gz | tar zxf -
	cd gputils-1.5.0 && mkdir -p install && ./configure --prefix=$$PWD/install --disable-html-doc && make install
	PATH=$$PWD/gputils-1.5.0/install/bin:$$PATH make tests

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
	${MAKE} ${@:.cmp=.asm} OPTS="--no-comments"
	diff -u ${@:.cmp=.ref} ${@:.cmp=.asm}

%.icmp: %.iref never
	${RM} ${@:.icmp=.asm}
	${MAKE} ${@:.icmp=.asm} OPTS="--no-comments -a"
	diff -u ${@:.icmp=.iref} ${@:.icmp=.asm}

%.newref: never
	${RM} ${@:.newref=.asm}
	${MAKE} ${@:.newref=.asm} OPTS="--no-comments"
	cp -p ${@:.newref=.asm} ${@:.newref=.ref}
	${RM} ${@:.newref=.asm}
	${MAKE} ${@:.newref=.asm} OPTS="--no-comments -a"
	cp -p ${@:.newref=.asm} ${@:.newref=.iref}

%.load: %.hex
	${PYTHON} utils/monitor.py --program --port=${PORT} --speed=${SPEED} $<

tests/interrupts.asm: tests/interrupts.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts.fs

tests/interrupts2.asm: tests/interrupts2.fs
	${PYTHON} ${COMPILER} -i ${FLAGS} tests/interrupts2.fs
