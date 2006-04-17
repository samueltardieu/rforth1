needs lib/tty-rs232.fs

0xDEAD constant magic

: magic-present? ( -- f ) 0 eeprom@ magic = ;
: reset-counters ( -- ) magic 0 eeprom! 1 2 eepromc! ;
: read-and-inc ( -- n ) 2 eepromc@ dup 1+ 2 eepromc! ;

: first ( -- ) reset-counters ." Please reset and reexecute" cr ;
: others ( -- ) ." I have been executed " read-and-inc . ."  times" cr ;
: main ( -- ) cr magic-present? 0= if first else others then ;
