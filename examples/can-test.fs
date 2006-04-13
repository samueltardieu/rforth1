needs lib/can.fs
needs lib/tty.fs

\ To test loopback, use same local and remote address and uncomment
\ the call to can_loopback

0x8060 constant local_addr
\ 0x8060 constant remote_addr
0x80E0 constant remote_addr

: main 
    can_init
    0xFF00 local_addr can_set_mask
    \ can_loopback
\    REQOP2 bit-set
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
    
    begin
        can_msg_present
        if
            can_receive
            emit
            drop
        then
        key?
        if
            remote_addr
            key
            ." _" dup emit
            can_emit
        then
    again ;

