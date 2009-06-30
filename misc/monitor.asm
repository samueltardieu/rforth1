;;; Monitor for a pic18f258 microcontroller.
;;; This monitor initializes the serial port, then takes two kind of commands
;;; that can be executed on site:
;;;   1) write something in flash memory after erasing it
;;;         Waaaaaaxxxxxx (3 bytes address and 64 bytes of data)
;;;   2) jump to an address
;;;         Xaaaaaa
;;;   3) read an address content
;;;         Raaaaaa
;;; This should be enough to let one up and running.
;;; 
;;; This monitor has been written by Samuel Tardieu <sam@rfc1149.net>
;;; and has been placed in the public domain.
;;; 
;;; Do not forget to customize the monitor to your need:
;;;   - oscillator type
;;;   - serial port settings
;;;   - led handling

include "p18f258.inc"
	
	processor	18f258
	__config	_CONFIG1H, _OSCS_OFF_1H & _HSPLL_OSC_1H
	__config	_CONFIG2L, _BOR_ON_2L & _PWRT_ON_2L & _BORV_45_2L
	__config	_CONFIG2H, _WDT_OFF_2H
	__config	_CONFIG4L, _DEBUG_OFF_4L & _LVP_OFF_4L & _STVR_ON_4L
	__config	_CONFIG5L, _CP0_OFF_5L & _CP1_OFF_5L
	__config	_CONFIG5H, _CPB_OFF_5H & _CPD_OFF_5H
	__config	_CONFIG6L, _WRT0_OFF_6L & _WRT1_OFF_6L
	__config	_CONFIG6H, _WRTB_OFF_6H & _WRTC_OFF_6H & _WRTD_OFF_6H
	__config	_CONFIG7L, _EBTR0_OFF_7L & _EBTR1_OFF_7L
	__config	_CONFIG7H, _EBTRB_OFF_7H
	
	
	org 0x0
	bra	start

	org 0x8
	goto	0x2008

	org 0x18
	goto	0x2018
	
start:
	rcall	leds_init
	rcall	serial_init
	rcall	wait_for_key
	rcall	welcome
mainloop:
	rcall	command
	bra	mainloop

	;; Leds initialization
leds_init:
	clrf	LATA
	btg	LATA,0		; Lit led A0
	movlw	H'7'
	movwf	ADCON1		; Use port A as GPIO
	movlw	H'F0'
	movwf	TRISA		; Ports A0 to A3 are outputs for leds
	return

	;; Serial port initialization
serial_init:
	bcf	TRISC,6		; Set TX as an output
	movlw	D'21'		; Select 115200 bps at 40MHz
	movwf	SPBRG
	bsf	TXSTA,BRGH
	bcf	TXSTA,SYNC
	bsf	RCSTA,SPEN
	bsf	TXSTA,TXEN
	bsf	RCSTA,CREN
	return
	
	;; Read a command and execute it
command:
	btg	LATA,1		; Toggle led A1
	rcall	ok

command_no_prompt:	
	rcall	get_char
	
	xorlw	H'57'		; Command 'W'
	btfsc	STATUS,Z
	bra	command_write

	xorlw	H'F'		; Command 'X'
	btfsc	STATUS,Z
	bra	command_execute

	xorlw	H'A'		; Command 'R'
	btfsc	STATUS,Z
	bra	command_read

	xorlw	H'6'		; Command 'T'
	btfsc	STATUS,Z
	bra	command_sync
	
	bra	command		; No command recognized, retry

command_sync:
	movlw	H'21'		; ! is the answer to T
	rcall	put_char
	bra	command_no_prompt
	
	;; Read 3 bytes and display the content of a given address
command_read:
	btg	LATA,0		; Toggle led A0
	rcall	get_address
	rcall	arrow
	tblrd*
	movf	TABLAT,w
	bra	put_hexa

	;; Read 3 bytes and store them into TBLPTRU/H/L
get_address:
	rcall	get_hexa
	movwf	TBLPTRU
	rcall	get_hexa
	movwf	TBLPTRH
	rcall	get_hexa
	movwf	TBLPTRL
	return
	
	;; Read 3 bytes and jump to a given address
command_execute:
	btg	LATA,2		; Toggle led A2
	rcall	get_hexa
	movwf	PCLATU
	rcall	get_hexa
	movwf	PCLATH
	rcall	get_hexa
	movwf	PCL		; This instruction jumps to the given address

	;; Read an address on 3 bytes then 64 bytes and write them to flash
command_write:
	btg	LATA,3		; Toggle led A3
	rcall	get_address	
	rcall	start_of_buffer

	movlw	D'64'
	movwf	counter_bytes
command_write_get_byte:
	rcall	get_hexa
	movwf	POSTINC0
	decfsz	counter_bytes,f
	bra	command_write_get_byte

	rcall	flash_erase
	rcall	start_of_buffer
	bra	flash_write

	;; Position FSRO to start of buffer
start_of_buffer:	
	movlw	high(buffer)
	movwf	FSR0H
	movlw	low(buffer)
	movwf	FSR0L
	return
	
	;; Get a character from serial line into W with echo
get_char:
	clrwdt
	btfss	PIR1,RCIF
	bra	get_char
	movf	RCREG,w
	bra	put_char

	;; Set timer 0 on for a few seconds and wait until a key is pressed
	;; or launch the main program if none arrives
wait_for_key
	movlw	87
	movwf	T0CON
	clrf	TMR0H
	clrf	TMR0L
	bcf	INTCON,TMR0IF
wait_for_key_loop
	clrwdt
	btfsc	INTCON,TMR0IF
	goto	0x2000
	btfsc	PIR1,RCIF
	return
	bra	wait_for_key_loop
	
	;; Get an hexadecimal digit from serial line into W
get_hexa:
	rcall	get_nibble
	movwf	nibble
	swapf	nibble,f
	rcall	get_nibble
	iorwf	nibble,w
	return

	;; Get an hexadecimal nibble from serial line into W
get_nibble:
	rcall	get_char
	movwf	nibble_tmp
	btfss	nibble_tmp,4
	addlw	H'9'
	andlw	H'F'
	return

	;; Print the current address as recorded in TBLPTRU/H/L
put_flash_address:
	movf	TBLPTRU,w
	rcall	put_hexa
	movf	TBLPTRH,w
	rcall	put_hexa
	movf	TBLPTRL,w
	;; Fall through put_hexa
	
	;; Put a byte in hexadecimal form onto the serial line	
put_hexa:
	movwf	nibble_tmp
	swapf	nibble_tmp,w
	rcall	put_nibble
	movf	nibble_tmp,w
	;; Fall through put_nibble
	
	;; Put an hexadecimal nibble from W into serial line
put_nibble:
	andlw	H'0F'
	addlw	H'F6'
	btfsc	STATUS,C
	addlw	H'7'
	addlw	H'3A'
	;; Fall through put_char
		
	;; Put a character from W to serial line
put_char:
	clrwdt
	btfss	PIR1,TXIF
	bra	put_char
	movwf	TXREG
	return

	;; Erase 32 words of flash memory at TBLPTR
flash_erase:
	bsf	EECON1,FREE
	;; fallthrough flash_operate
	
	;; Perform a flash write or erase operation
flash_operate:	
	bcf	EECON1,CFGS
	bsf	EECON1,EEPGD
	bsf	EECON1,WREN
	bcf	INTCON,GIE
	movlw	H'55'
	movwf	EECON2
	movlw	H'AA'
	movwf	EECON2
	bsf	EECON1,WR
	nop
	bsf	INTCON,GIE
	return

	;; Write 32 words of flash memory starting from the address in
	;; FSR0 at address stored in TBLPTR
flash_write:
	tblrd*-		; Dummy write increment so that the last write
			; stays in the right page (writes will use +*)
	movlw	D'8'
	movwf	counter_blocks
flash_block_loop:
	movlw	D'8'
	movwf	counter_bytes
flash_byte_loop:
	rcall	cr
	movf	POSTINC0,w
	movwf	TABLAT
	tblwt+*
	rcall	put_flash_address
	
	decfsz	counter_bytes,f
	bra	flash_byte_loop
	
	rcall	flash_operate
	
	decfsz	counter_blocks,f
	bra	flash_block_loop
	bcf	EECON1,WREN
	return

welcome:
	movlw	upper(welmsg)
	movwf	TBLPTRU
	movlw	high(welmsg)
	movwf	TBLPTRH
	movlw	low(welmsg)
	movwf	TBLPTRL
	movlw	wellen
	;; Fallthrough put_strln

put_strln:
	rcall	put_str
	;; Fall through cr

cr:
	movlw	'\n'
	bra	put_char

ok:
	rcall	cr
	movlw	'o'
	rcall	put_char
	movlw	'k'
	rcall	put_char
	movlw	'>'
	rcall	put_char
	;; Fallback through put_space

put_space:
	movlw	' '
	bra	put_char
			
arrow:
	rcall	put_space
	movlw	'='
	rcall	put_char
	movlw	'>'
	rcall	put_char
	bra	put_space
			
	;; Put a string whose length is in W and whose content is in TBLPTRU/H/L
put_str:	
	movwf	put_str_tmp
put_str_loop:	
	tblrd*+
	movf	TABLAT,w
	rcall	put_char
	decfsz	put_str_tmp,f
	bra	put_str_loop
	return
	
	;; Those two routines are only here to test the loader. By calling
	;; them using the "execute" command, the execute command can be
	;; partially tested.
all_leds_on:
	movlw	H'F'
	movwf	PORTA
	return

all_leds_off:
	clrf	PORTA
	return

cblock 0
	counter_bytes,counter_blocks,nibble,nibble_tmp,put_str_tmp,buffer:64
endc

welmsg:
	da	"Welcome to rforth1 monitor at 115200"
wellen	equ	$-welmsg

end
