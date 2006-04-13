\ Most of the code here should be optimized in the core compiler
\ for special cases. However, the goal is to have it work rapidly even
\ if it is slow rather than working fast in a long time.


code op_plus
    intr-protect
    POSTDEC0 temp_x1 movff   \ Store high byte of 2nd argument in temp_x1
    POSTDEC0 ,w ,a movf   \ Store low byte of 2nd argument in W
    POSTDEC0 ,f ,a movf   \ TOS decrement
    POSTINC0 ,f ,a addwf   \ Add low byte and increment
    temp_x1 ,w ,a movf   \ Reload high byte of 2nd argument
    intr-unprotect
    INDF0 ,f ,a addwfc   \ Add high byte and carry
    return
;code

code +!
    POSTDEC0 FSR1H movff   \ Copy address to FSR1
    POSTDEC0 FSR1L movff
    POSTDEC0 ,w ,a movf   \ TOS decrement
    POSTINC0 ,w ,a movf   \ Low byte of constant into W and increment TOS
    POSTINC1 ,f ,a addwf   \ Add low bytes and increment
    POSTDEC0 ,w ,a movf   \ High byte in W
    INDF1 ,f ,a addwfc   \ Store result
    POSTDEC0 ,w ,a movf   \ TOS decrement
    return
;code

code c+!
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    POSTDEC0 ,w ,a movf
    POSTDEC0 ,w ,a movf
    INDF1 ,f ,a addwf
    return
;code

code op_minus
    intr-protect
    POSTDEC0 temp_x1 movff   \ Store high byte of 2nd argument in temp_x1
    POSTDEC0 ,w ,a movf   \ Store low byte of 2nd argument in W
    POSTDEC0 ,f ,a movf   \ TOS decrement
    POSTINC0 ,f ,a subwf   \ Subtract low byte and increment
    temp_x1 ,w ,a movf   \ Reload high byte of 2nd argument
    intr-unprotect
    INDF0 ,f ,a subwfb   \ Subtract high byte and borrow
    return
;code

code -!
    POSTDEC0 FSR1H movff   \ Copy address to FSR1
    POSTDEC0 FSR1L movff
    POSTDEC0 ,w ,a movf   \ TOS decrement
    POSTINC0 ,w ,a movf   \ Low byte of constant into W and increment TOS
    POSTINC1 ,f ,a subwf   \ Subtract low bytes and increment
    POSTDEC0 ,w ,a movf   \ High byte in W
    INDF1 ,f ,a subwfb   \ Store result
    POSTDEC0 ,w ,a movf   \ TOS decrement
    return
;code

code and
    intr-protect
    POSTDEC0 temp_x1 movff   \ Store high byte of 2nd argument in temp_x1
    POSTDEC0 ,w ,a movf   \ Store low byte of 2nd argument in W
    POSTDEC0 ,f ,a movf   \ TOS decrement
    POSTINC0 ,f ,a andwf   \ And low byte and increment
    temp_x1 ,w ,a movf   \ Reload high byte of 2nd argument
    intr-unprotect
    INDF0 ,f ,a andwf   \ And high byte and carry
    return
;code

code or
    intr-protect
    POSTDEC0 temp_x1 movff   \ Store high byte of 2nd argument in temp_x1
    POSTDEC0 ,w ,a movf   \ Store low byte of 2nd argument in W
    POSTDEC0 ,f ,a movf   \ TOS decrement
    POSTINC0 ,f ,a iorwf   \ Or low byte and increment
    temp_x1 ,w ,a movf   \ Reload high byte of 2nd argument
    intr-unprotect
    INDF0 ,f ,a iorwf   \ Or high byte and carry
    return
;code

code xor
    intr-protect
    POSTDEC0 temp_x1 movff   \ Store high byte of 2nd argument in temp_x1
    POSTDEC0 ,w ,a movf   \ Store low byte of 2nd argument in W
    POSTDEC0 ,f ,a movf   \ TOS decrement
    POSTINC0 ,f ,a xorwf   \ Xor low byte and increment
    temp_x1 ,w ,a movf   \ Reload high byte of 2nd argument
    intr-unprotect
    INDF0 ,f ,a xorwf   \ Xor high byte and carry
    return
;code

code op_2>1
    POSTDEC0 ,w ,a movf
    POSTDEC0 ,w ,a movf   \ Get low byte of msb
    INDF0 ,a movwf   \ Store it as lsb
    return
;code inline

code 1>2
    INDF0 ,w ,a movf   \ Load MSB into W
    INDF0 ,a clrf   \ Clear high byte of LSB
    PREINC0 ,a movwf   \ Store MSB into low byte of TOS
    PREINC0 ,a clrf   \ Clear high byte of MSB
    return
;code

code lsb
    INDF0 ,a clrf   \ Clear high byte of LSB
    return
;code inline

code msb
    POSTDEC0 ,w ,a movf   \ Load MSB into W and decrement TOS
    POSTINC0 ,a movwf   \ Store MSB into low byte and increment TOS
    INDF0 ,a clrf   \ Clear high byte of MSB
    return
;code inline

code invert
    POSTDEC0 ,f ,a comf   \ Invert high byte and decrement TOS
    POSTINC0 ,f ,a comf   \ Invert low byte and increment TOS
    return
;code inline

code negate
    POSTDEC0 ,f ,a comf   \ Invert high byte and decrement TOS
    POSTINC0 ,a negf   \ Negate low byte and increment TOS
    0 movlw
    INDF0 ,f ,a addwfc   \ Increment high byte if carry is set
    return
;code

code 0<
    -1 movlw   \ Assume positive result
    POSTDEC0 7 ,a btfss   \ Test high bit of MSB and point to LSB
    0 movlw   \ Negative result
    POSTINC0 ,a movwf   \ Store result in LSB
    INDF0 ,a movwf   \ and in MSB
    return
;code

: op_= ( n1 n2 -- f ) xor 0= ; inline
: <> ( n1 n2 -- f ) xor 0<> ; inline
: 0> ( n -- f ) negate 0< ; inline
: 0>= ( n1 n2 -- f ) 0< 0= ; inline
: 0<= ( n1 n2 -- f ) negate 0>= ; inline
: U< ( u1 u2 -- f ) - 0< ; inline
: U> ( u1 u2 -- f ) swap U< ; inline
: U>= ( u1 u2 -- f ) U< 0= ; inline
: U<= ( u1 u2 -- f ) swap U>= ; inline

code 2dupxor>w ( u1 u2 -- h[u1]^h[u2]/w )
  POSTDEC0 ,f ,a movf
  POSTDEC0 ,f ,a movf
  POSTINC0 ,w ,a movf
  PREINC0 ,w ,a xorwf
  return
;code

: <
  2dupxor>w WREG 7 bit-set? if      \ Different signs
    drop 0<
  else                              \ Unsigned comparaison
    U<
  then
;

: >=
  2dupxor>w WREG 7 bit-set? if      \ Different signs
    drop 0>=
  else                              \ Unsigned comparaison
    U>=
  then
;

: > swap < ;
: <= swap >= ;

\ This way of coding avoids a costly dup
: abs ( n -- u ) INDF0 7 bit-set? if negate then ; inline

code 2/
    INDF0 ,w ,a rlcf   \ Acquire carry
    POSTDEC0 ,f ,a rrcf   \ Shift MSB
    POSTINC0 ,f ,a rrcf   \ Shift LSB
    return
;code inline

code 2*
    C ,a bcf   \ Clear carry
    POSTDEC0 ,w ,a movf   \ Point onto LSB
    POSTINC0 ,f ,a rlcf   \ Shift LSB
    INDF0 ,f ,a rlcf   \ Shift MSB
    return
;code

code (*)
    temp_x1 ,w ,a movf
    temp_x2 ,a mulwf
    PRODL PREINC0 movff
    PRODH temp_x3 movff
    temp_x2 1 + ,a mulwf
    PRODL ,w ,a movf
    temp_x3 ,f ,a addwf
    temp_x1 1 + ,w ,a movf
    temp_x2 ,a mulwf
    PRODL ,w ,a movf
    temp_x3 ,w ,a addwfc
    intr-unprotect
    PREINC0 ,a movwf
    return
;code

: * intr-protect temp_x1 ! temp_x2 ! (*) ;

code op_1+
    POSTDEC0 ,w ,a movf
    POSTINC0 ,f ,a infsnz
    INDF0 ,f ,a incf
    return
;code inline

code swapf-lsb
    POSTDEC0 ,w ,a movf   \ Point onto LSB
    POSTINC0 ,f ,a swapf   \ Swap LSB and restore TOS
    return
;code inline

: depth intr-protect FSR0L @ temp_x1 ! temp_x1 @ intr-unprotect 0x5f - 2/ ;

: pick negate depth + 2* 0x5c + @ ;

