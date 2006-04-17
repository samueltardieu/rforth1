needs lib/canlib.fs
needs lib/tty-rs232.fs

\ To test loopback, use same local and remote address and uncomment
\ the call to can-loopback

0x80E0 constant local-addr
0x8060 constant remote-addr
0b11111111111 constant addr-mask

64 constant buffer-size

create buffer buffer-size allot
variable next-write
variable next-read

: inc-wrap ( x -- y ) 1+ dup buffer-size >= if drop 0 then ;

: >buffer ( n -- a ) buffer + ;

: bufferc! ( c -- )
  next-write @ >buffer c!
  next-write @ inc-wrap next-write !
;

: bufferc@ ( -- c )
  next-read @ >buffer c@
  next-read @ inc-wrap next-read !
;

: buffer-not-empty? ( -- f ) next-read @ next-write @ <> ;

: serial-to-can ( -- ) key remote-addr can-emit-1 ;
: can-to-buffer ( -- ) can-receive can-msg-0 c@ bufferc! ;
: buffer-to-serial ( -- ) txif bit-set? if bufferc@ emit then ;

: step ( -- )
  key? if serial-to-can then
  can-msg-present? if can-to-buffer then
  buffer-not-empty? if buffer-to-serial then
;

: mainloop ( -- ) begin step again ;

: main ( -- )
  can-init
  can-config local-addr 0 can-set-filter addr-mask 0 can-set-mask can-normal
  \ can-loopback
    ." CAN bootloader relay\n"
    ." RXM0SIDL " RXM0SIDL @ . cr
    ." RXM0SIDH " RXM0SIDH @ . cr
    ." RXM0EIDL " RXM0EIDL @ . cr
    ." RXM0EIDH " RXM0EIDH @ . cr
    ." RXF0SIDL " RXF0SIDL @ . cr
    ." RXF0SIDH " RXF0SIDH @ . cr
    ." RXF0EIDL " RXF0EIDL @ . cr
    ." RXF0EIDH " RXF0EIDH @ . cr
    ." RXF1SIDL " RXF1SIDL @ . cr
    ." RXF1SIDH " RXF1SIDH @ . cr
    ." RXF1EIDL " RXF1EIDL @ . cr
    ." RXF1EIDH " RXF1EIDH @ . cr
    ." BRGCON1 " BRGCON1 @ . cr
    ." BRGCON2 " BRGCON2 @ . cr
    ." BRGCON3 " BRGCON3 @ . cr
    ." CIOCON " CIOCON @ . cr
    ." TXB0DLC " TXB0DLC @ . cr
\    CANCON 7 bit-clr

\    2 TXB0DLC c!
    ." TXB0DLC " TXB0DLC @ . cr
     
\    0 TXB0EIDH c!
\    0 TXB0EIDL c!
    
  mainloop ;
