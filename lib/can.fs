TRISB constant can_port
3 constant can_pin_rx
2 constant can_pin_tx

0x09 constant can_BRGCON1
0x90 constant can_BRGCON2
0x02 constant can_BRGCON3
0x40 constant can_CIOCON

\ mask
0x00 constant can_RXM0SIDH
0x00 constant can_RXM0SIDL
0x00 constant can_RXM0EIDH
0x00 constant can_RXM0EIDL

\ filter
0x00 constant can_RXF0SIDH
0x00 constant can_RXF0SIDL
0x00 constant can_RXF0EIDH
0x00 constant can_RXF0EIDL

0x00 constant can_RXF1SIDH
0x00 constant can_RXF1SIDL
0x00 constant can_RXF1EIDH
0x00 constant can_RXF1EIDL

: can_init ( -- )
  
  TRISB 3 bit-set
  TRISB 2 bit-clr

  REQOP2 bit-set   \ config mode
  
  can_RXM0SIDH RXM0SIDH c!
  can_RXM0SIDL RXM0SIDL c!
  can_RXM0EIDH RXM0EIDH c!
  can_RXM0EIDL RXM0EIDL c!
  
  can_RXF0SIDH RXF0SIDH c!
  can_RXF0SIDL RXF0SIDL c!
  can_RXF0EIDH RXF0EIDH c!
  can_RXF0EIDL RXF0EIDL c!
  
  can_RXF0SIDH RXF1SIDH c!
  can_RXF0SIDL RXF1SIDL c!
  can_RXF0EIDH RXF1EIDH c!
  can_RXF0EIDL RXF1EIDL c!
  
  can_BRGCON1 BRGCON1 c!
  can_BRGCON2 BRGCON2 c!
  can_BRGCON3 BRGCON3 c!
  can_CIOCON CIOCON c!
  
  0 TXB0EIDH c!
  0 TXB0EIDL c!
  
  2 TXB0DLC c!
  
  REQOP2 bit-clr   \ out of config mode
  REQOP1 bit-clr
  REQOP0 bit-clr   \ normal mode
;

: can_set_mask ( m f -- ) 
  REQOP2 bit-set   \ config mode
  1>2 RXF0SIDH c! RXF0SIDL c!
  1>2 RXM0SIDH c! RXM0SIDL c!
  REQOP2 bit-clr   \ out of config mode
;
  
: can_loopback
  REQOP1 bit-set
;

: can_normal
  REQOP1 bit-clr
;

: can_emit ( a u -- )
  begin TXB0TXREQ bit-clr? until
  1>2 TXB0D1 c! TXB0D0 c!
  1>2 TXB0SIDH c! TXB0SIDL c!
  TXB0TXREQ bit-set
;

: can_receive ( -- a u )
  begin RXB0RXFUL bit-set? until
  RXB0SIDL @ RXB0SIDH @ 2>1
  RXB0D0 @ RXB0D1 @ 2>1
  RXB0RXFUL bit-clr
;

: can_msg_present ( -- m )
  RXB0RXFUL bit-set?
; inline

: can_txerror ( -- e )
  begin TXB0TXREQ bit-clr? until
  TXB0TXREQ bit-set?
;
