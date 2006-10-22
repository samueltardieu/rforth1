\ init_runtime definition does not end with a return as it will be completed
\ by other initializations
code init_runtime
     1 movlb
;code

code init_stack
     0x5f movlw
     FSR0L ,a movwf
     FSR0H ,a clrf
     return
;code inline

code init_rstack
     0xbf movlw
     FSR2L ,a movwf
     FSR2H ,a clrf
     return
;code inline

\ Temporary variables used for computations
variable temp_x1
variable temp_x2
variable temp_x3

\ Temporary variable to save GIE
cvariable temp_gie

code dup
      -1 movlw
      PLUSW0 PREINC0 movff
      PLUSW0 PREINC0 movff
      return
;code

code 2dup
      -3 movlw
      PLUSW0 PREINC0 movff
      PLUSW0 PREINC0 movff
      PLUSW0 PREINC0 movff
      PLUSW0 PREINC0 movff
      return
;code

code op_normalize_z
     -1 movlw
     Z ,a btfsc
     1 addlw
     PREINC0 ,a movwf
     PREINC0 ,a movwf
     return
;code

code op_normalize
     POSTDEC0 ,w ,a movf
     POSTDEC0 ,w ,a iorwf
     op_normalize_z call
     return
;code inline

code op_zeroeq_z
     -1 movlw
     Z ,a btfss
     1 addlw
     PREINC0 ,a movwf
     PREINC0 ,a movwf
     return
;code

code op_zeroeq
     POSTDEC0 ,w ,a movf
     POSTDEC0 ,w ,a iorwf
     op_zeroeq_z call
     return
;code inline

\ The result of the bit mask will be stored and W, and the FSR0 will point
\ on the byte following the current stack value.
code op_bit_mask_to_w
     \ Make INDF0 point onto the TOS low-byte and add 1
     POSTDEC0 ,w ,a movf
     INDF0 ,f ,a incf
     \ Set W to initial value 1
     1 movlw
label op_bit_mask_loop
     \ Decrement TOS and jump out of the loop if zero
     INDF0 ,f ,a dcfsnz
     return
     \ Multiply W by 2 and loop
     WREG ,f ,a rlncf
     op_bit_mask_loop goto
;code

code op_bit_mask
     op_bit_mask_to_w call
     POSTINC0 ,a movwf
     INDF0 ,a clrf
     return
;code

code op_bit_test_common
     op_bit_mask call
     POSTDEC0 ,w ,a movf
     POSTDEC0 ,w ,a movf
     POSTDEC0 FSR1H movff
     POSTDEC0 FSR1L movff
     INDF1 ,w ,a andwf
     return
;code

code op_bit_set
    op_bit_mask_to_w call
    POSTDEC0 ,f ,a movf   \ Restore the offset by 1
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    INDF1 ,f ,a iorwf
    return
;code

code op_bit_clr
    op_bit_mask_to_w call
    POSTDEC0 ,f ,a movf   \ Restore the offset by 1
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    WREG ,f ,a comf
    INDF1 ,f ,a andwf
    return
;code

code op_bit_toggle
    op_bit_mask_to_w call
    POSTDEC0 ,f ,a movf   \ Restore the offset by 1
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    INDF1 ,f ,a xorwf
    return
;code

code op_bit_set_q
     op_bit_test_common call
     Z ,a btfss   \ If Z is set, W already contains 0
     -1 movlw
     PREINC0 ,a movwf
     PREINC0 ,a movwf
     return
;code

code op_bit_clr_q
     op_bit_test_common call
     0 movlw
     Z ,a btfsc
     -1 movlw
     PREINC0 ,a movwf
     PREINC0 ,a movwf
     return
;code

forward eeprom!
code op_store
     INDF0 4 ,a btfsc
     eeprom! goto
     POSTDEC0 FSR1H movff
     POSTDEC0 FSR1L movff
     POSTDEC0 PREINC1 movff
     POSTDEC1 ,w ,a movf
     POSTDEC0 INDF1 movff
     return
;code

forward eepromc!
code op_cstore
     INDF0 4 ,a btfsc
     eepromc! goto
     POSTDEC0 FSR1H movff
     POSTDEC0 FSR1L movff
     POSTDEC0 ,w ,a movf   \ Trash high byte
     POSTDEC0 INDF1 movff
     return
;code

forward eeprom@
forward flash@
code op_fetch_tos
    INDF0 7 ,a btfsc
    flash@ goto
    INDF0 4 ,a btfsc
    eeprom@ goto
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    POSTINC1 PREINC0 movff
    INDF1 PREINC0 movff
    return
;code

forward eepromc@
forward flashc@
code op_cfetch_tos
    INDF0 7 ,a btfsc
    flashc@ goto
    INDF0 4 ,a btfsc
    eepromc@ goto
    POSTDEC0 FSR1H movff
    POSTDEC0 FSR1L movff
    INDF1 PREINC0 movff
    PREINC0 ,a clrf
    return
;code

code drop
    POSTDEC0 ,f ,a movf
    POSTDEC0 ,f ,a movf
    return
;code inline

code >r
    POSTDEC0 PREINC2 movff
    POSTDEC0 PREINC2 movff
    return
;code inline

code r>
    POSTDEC2 PREINC0 movff
    POSTDEC2 PREINC0 movff
    return
;code inline

code r@
    POSTDEC2 PREINC0 movff
    POSTINC2 PREINC0 movff
    return
;code inline

code rdrop
    POSTDEC2 ,f ,a movf
    POSTDEC2 ,f ,a movf
    return
;code inline

: i r@ ; inline 

: swap intr-protect temp_x1 ! >r temp_x1 @ intr-unprotect r> ;

: 2drop drop drop ;
: 2>r swap >r >r ;    
: 2r> r> r> swap ;

code cr@
    INDF2 PREINC0 movff
    PREINC0 ,a clrf
    return
;code inline

: over >r dup r> swap ;

: nip swap drop ;

: execute jump ; no-inline