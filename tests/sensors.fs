\ Source of the color sensors command card 
\ 
\ 

needs lib/tty.fs
needs lib/can.fs


\ Port naming
ADCON0 2 bit GO/DONE
PORTB 0 bit led1
PORTB 1 bit led2
LATB 4 bit button

\ Sensors leds 
PORTA 0 bit leds

\ Status flag
0 value leds-on?
0 value calibrated?

\ Color code
0 constant UNKNOWN
1 constant BEIGE
2 constant BROWN
3 constant WHITE

\ *************
\ CAN constant
\ XXXXX define the address
0b0000000000000000 constant local-addr
0b0111110000000000 constant remote-addr
0b0111110000000000 constant addr-mask
0b0000001111100000 constant private-mask

\ Action Mask
0b0000001100000000 constant request-values-mask
0b0000000010000000 constant request-color-mask
0b0000000001100000 constant set-thresholds-mask


forward can-unknown-msg
forward can-transmit-values
forward can-transmit-color
forward can-free-buffer

\ forward serial-transmit-color
\ forward serial-transmit-values
\ *************

\ variable avg-beige
\ variable avg-white
\ variable avg-brown


\ Color thresholds
variable brown-beige-threshold
variable beige-white-threshold


forward request-values


\ ***************************************
\ Useful words 
: 8* 4* 4* ;

\ ***************************************
\ : reset-all ( -- ) 
\	0 avg-beige !
\	0 avg-white !
\	0 avg-brown !
\ ;


\ ***************************************
\ Setup of the CAN bus
: can-setup ( -- )
	can_init addr-mask local-addr can_set_mask
;

\ Switch on the sensors leds 
: leds-on ( -- ) 
	leds bit-set -1 to leds-on?
;

\ Switch off the sensors leds
: leds-off ( -- )
	leds bit-clr 0 to leds-on?
;

\ 
: timer0-reached ( -- flag )
	TMR0IF bit-set? if TMR0IF bit-clr 1 else 0 then
;

\ Change the AD channel
: select-channel ( channel -- )
	ADCON0 c@ $c7 and swap 8* or ADCON0 c!
;

\ Get the AD value
: conversion ( -- result )
	GO/DONE bit-set begin GO/DONE bit-clr? until
	ADRESL c@ ADRESH c@ 2>1 
;
   
\ Measures
: 4-measures ( -- result  ) 
	0 
	4 cfor conversion + cnext
;

\ Measure on a given channel
: channel-measure ( channel -- result )
	select-channel
	leds-on  4-measures
	leds-off 4-measures
	-
;

\ XXXX Check port on schematics
: button-pressed? ( -- ) button bit-set? ; inline

\ Calibration
\ : calibration ( -- )
\	\ First : calibration for the brown
\	led1 bit-clr 0
\	4 cfor 
\		cr@ 1- channel-measure +	
\	cnext 
\	4/ avg-brown !
\	led1 bit-set
\	begin button-pressed? until
\	
\	\ Second : calibration of the beige
\	led1 bit-clr
\	0
\	4 cfor 
\		cr@ 1- channel-measure +
\	cnext
\	4/ avg-beige !
\	led1 bit-set
\	begin button-pressed? until
\	
\	\ Third : calibration of the white
\	led1 bit-clr
\	4 cfor
\		cr@ 1- channel-measure avg-white +!
\		led1 bit-set
\		begin button-pressed? until
\		led1 bit-clr
\	cnext
\	avg-white @ 4/ avg-white ! 
\	1 to calibrated?
\ ;

\ Computation of the thresholds
\ : threshold-computation ( -- )
\	calibrated? if 
\		avg-beige @ dup avg-white @ + 2/ bw-threshold !
\		avg-brown @ + 2/ bb-threshold !
\	\ else lire en eeprom
\	then
\ ;


\ Determine the color
\ n value from a channel-measure
: determine-color ( n -- v  )
	dup 
	brown-beige-threshold < if
		\ brown detected
		\ TODO traitement
		drop BROWN lsb  exit then
	
	beige-white-threshold > if
		\ white detected
		\ TODO traitement
		WHITE lsb exit
	else
		\ beige detected
		\ TODO traitement
		BEIGE lsb exit
	then
;	

: channel-determine-color ( channel -- c )
	channel-measure determine-color
;

\ ******************

\ Read the CAN buffer and set the color thresholds
\ XXXXX
: set-thresholds ( -- ) 
	RXB0D0 c@ RXB0D1 c@ 2>1 beige-white-threshold c!
	RXB0D2 c@ RXB0D3 c@ 2>1 brown-beige-threshold c!
;

\ Read the CAN message and call the action defined in the message
: handle-can-message ( -- ) 
	\ TODO
	can_msg_present if begin RXB0RXFUL bit-set? until then \ Wait 
	RXB0SIDL c@ RXB0SIDH c@ 2>1 \ Read the header
	dup private-mask and	  \ Apply the mask to extract the private part of the message 	
	\ Action
	dup request-color-mask  = if
		drop
		remote-addr 1>2 can-transmit-color
		can-free-buffer exit then
	dup request-values-mask = if 
		drop
		remote-addr 1>2 can-transmit-values
		can-free-buffer exit then
	dup set-thresholds-mask = if
		drop
		set-thresholds 
		can-free-buffer exit then
	can-unknown-msg can-free-buffer
;	


\ Set the payload
: set-payload ( n -- )
	TXB0DLC c!
;

\ Free the CAN input buffer
: can-free-buffer ( -- )
	RXB0RXFUL bit-clr
;

\ Handle the unknown CAN messages
: can-unknown-msg ( -- )
;

\ Transmit the color on the CAN bus
: can-transmit-color ( id_l id_h -- )
	4 set-payload
	TXB0SIDH c! TXB0SIDL c! \ Set identifier
	begin TXB0TXREQ bit-clr? until
	4 cfor
		cr@ 1- channel-determine-color 
	cnext
	TXB0D3 c! TXB0D2 c! TXB0D1 c! TXB0D0 c!	\ Fill the output buffer
	TXB0TXREQ bit-set	
;

\ Transmit the sensors values on the CAN bus
: can-transmit-values ( id_l id_h-- )
	\ XXXXX
	8 set-payload
	TXB0SIDH c! TXB0SIDL c! \ Set identifier
	begin TXB0TXREQ bit-clr? until
	4 cfor
		cr@ 1- select-channel conversion  \ Get the color
	cnext
	1>2 TXB0D7 c! TXB0D6 c! 1>2 TXB0D5 c! TXB0D4 c!	\ Fill the output buffer
	1>2 TXB0D3 c! TXB0D2 c! 1>2 TXB0D1 c! TXB0D0 c!	\ Fill the output buffer
	TXB0TXREQ bit-set	
;

\ Transmit the sensors values to the serial port
: serial-transmit-values ( -- )
	4 cfor 
		cr@ 1- dup select-channel conversion swap 
		." Channel" . ." :" .
	cnext
;

\ Transmit the result on the serial port
: serial-transmit-color ( -- )
	4 cfor 
		cr@ 1- channel-determine-color 
		." Channel " cr@ 1- . ." :" . cr
	cnext		
;

\ premier jet
: step ( -- )
	serial-transmit-color
;

\ Init word	
: init ( -- )
	\ Analog-digital converter
	$81 ADCON0 c! 
	$42 ADCON1 c!

	\ Port setup
	TRISB 0 bit-clr \ Led 1
	TRISB 1 bit-clr \ Led 2
	TRISB 3 bit-clr \ Sensors' leds 
	TRISB 4 bit-clr \ Button
	TRISA 0 bit-clr \

	\ Timers setup
	$c5 T0CON c!
	
;
: greetings ." Welcome to the sensors program" cr ;

: main 
	init
	greetings
	\ calibration
	begin step again
;

