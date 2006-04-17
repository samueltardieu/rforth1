needs lib/tty-rs232.fs

variable x

: init ." x <- " dup . cr x ! ;
: test1+ ." x @ 1+ = " x @ 1+ . cr ;
: test2+ ." x @ 2 + = " x @ 2 + . cr ;
: test1- ." x @ 1- = " x @ 1- . cr ;
: test2- ." x @ 2 - = " x @ 2 - . cr ;
: test100+ ." x @ 100 + = " x @ 0x100 + . cr ;
: test200+ ." x @ 200 + = " x @ 0x200 + . cr ;
: test100- ." x @ 100 - = " x @ 0x100 - . cr ;
: test200- ." x @ 200 - = " x @ 0x200 - . cr ;

: tests init test1+ test2+ test1- test2- test100+ test200+ test100- test200- ;
: main 0x1234 tests 0x20ff tests 0x2000 tests ;
