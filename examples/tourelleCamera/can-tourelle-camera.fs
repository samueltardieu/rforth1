\
\ Interface CAN Carte Moteurs
\ AREABOT
\
\ This example uses an obsolete CAN library and will not compile anymore


needs lib/can.fs
needs examples/allegroCommon.fs 

forward handle-stop
forward handle-free
forward handle-back
forward handle-forth

variable acc
variable dec
variable vmax
variable tvmax
variable v




\ masks
\ P<add><pri><non>
0b0111110000000000 constant addr_mask 
0b0000001111100000 constant private_mask 
0b0010000000000000 constant local_addr
0b0000000010100000 constant both_card_addr
0b0000000011100000 constant forth_mask
0b0000000011000000 constant back_mask
0b0000000000000000 constant free_mask
0b0000000010000000 constant stop_mask

: can-setup 
    can_init
    addr_mask local_addr can_set_mask
; 

\ Chooses the message payload
: set-payload TXB0DLC c! ;  


\ Frees the receiving buffer 
: free-buffer RXB0RXFUL bit-clr ;

\ reads the standard identifier
: read-identifier ( -- n )
  begin RXB0RXFUL bit-set? until
  RXB0SIDL @ RXB0SIDH @ 2>1
;

\ reads the payload
: read-payload ( -- )
  RXB0D0 c@ acc   c! 
  RXB0D1 c@ RXB0D2 c@ 2>1 vmax !  
  RXB0D3 c@ tvmax c! 
  RXB0D4 c@ dec   c! 

  acc c@ . cr
  vmax @ . cr
  tvmax c@ . cr
  dec c@ . cr
;
  
\ send a can message of payload 0
: emit0 ( i -- ) 
  0 set-payload
  begin TXB0TXREQ bit-clr? until
  1>2 TXB0SIDH c! TXB0SIDL c!
  TXB0TXREQ bit-set
;

\ send a can message of payload 5
: emit5 ( i d1 d16b d4 d5 -- )
  5 set-payload
  begin TXB0TXREQ bit-clr? until
  TXB0D4 c! TXB0D3 c! 1>2 TXB0D2 c! TXB0D1 c! TXB0D0 c!
  1>2 TXB0SIDH c! TXB0SIDL c!
  TXB0TXREQ bit-set
;

: send-stop
." Sent stop" cr
local_addr stop_mask or  emit0
; inline
 
: send-free
." Sent free" cr
local_addr free_mask or  emit0 
; inline 

: send-forth
." Sent forth" cr
\ stdid acc vmax tvmax dec payload
local_addr forth_mask or read8 dup . cr read16 dup . cr 50 8 emit5 
; inline

: send-back
." Sent back" cr
\ stdid acc vmax tvmax dec payload
local_addr back_mask or  read8 dup . cr read16 dup . cr 50 8 emit5
; inline


: handle-key 
        key 
        dup [CHAR] n = if drop MS1 bit-toggle ." MS1 toggled" cr exit then 
        dup [CHAR] m = if drop MS2 bit-toggle ." MS2 toggled" cr exit then 
        dup [CHAR] d = if drop ." v=" v @ . cr exit then 
        dup [CHAR] l = if drop ." Loopback on" cr can_loopback exit then 
        dup [CHAR] k = if drop ." Loopback off" cr can_normal exit then 
        dup [CHAR] 1 = if drop send-stop  exit then 
        dup [CHAR] 2 = if drop send-free  exit then
        dup [CHAR] 3 = if drop send-forth exit then
        	[CHAR] 4 = if  send-back  exit then   
		depth . cr \ if you press any other key you get the stack size 
;

: handle-message
		." Message received" cr
		dup .
		private_mask and \ we only test the private identifier 
		dup forth_mask = if drop handle-forth exit then
		dup back_mask  = if drop handle-back exit then
		dup free_mask  = if drop handle-free exit then
			stop_mask  = if handle-stop exit then
		." Unknown message" cr
;

: handle-can
	can_msg_present if read-identifier handle-message free-buffer then
    key? if handle-key then 
;

