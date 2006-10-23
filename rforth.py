#! /usr/bin/env python
#
# (c) 2005-2006 Samuel Tardieu <sam@rfc1149.net>
#
# rforth1 is released under the GNU General Public License (see the
# file COPYING in this directory)

"""Forth compiler targetting the PIC18Fxxx microcontrollers family.

Memory usage:
   - BSR always points onto bank 1 where variables will be preferably stored
   - for a 16 bits value, low byte is stored at the lowest address
   - data stack is indexed by FSR0; stack grows upward; low byte is pushed
     first and high byte next; INDF0 points onto the latest high byte pushed
   - data stack is located starting at 0x60 to let short-addressable part
     of memory bank 0 free for user use
   - FSR1 is used for indirect memory access
   - FSR2 is used for a return stack located starting at 0xc0 in bank 0;
     data are stored MSB first on this stack to ease transfers between
     stacks
   - RAM cells are using address between 0x0000 and 0x0FFF. EEPROM will
     be designated as an address between 0x1000 and 0x1FFF. Flash will
     use bounds of 0x8000 and 0xFFFF.
   - core defined variables are located between 0x000 and 0x05f (in the first
     bank) and are not zero-initialized; they are typically temporary
     variables used in computations
   - user-defined variables are located starting at 0x0100"""

import optparse, os, sre, string, sys

def parse_number(str):
  """Parse a string and return a Number object with the right number and the
  preferred base for representation."""
  sign = 1
  while str[:1] == '-': sign, str = -sign, str[1:]
  for prefix, base in [('$', 16), ('0x', 16), ('0b', 2), ('', 10)]:
    if str[:len(prefix)] == prefix:
      try: return Number(sign * int(str[len(prefix):], base), base)
      except: return None
  return None

def stderror(str):
  """Write a string on standard error and add a carriage return."""
  sys.stderr.write("%s\n" % str)
  
def warning(str):
  """Print a warning on standard error."""
  stderror('WARNING: ' + str)
  
def error(str):
  """Print a fatal error on standard error."""
  stderror('ERROR: ' + str)

def make_tuple(insn, parameters):
  return insn, tuple(parameters)

def octet(n):
  """Check whether n is between 0 and 255."""
  return n >= 0 and n <= 255

class LiteralValue:
  """Represent any literal value that can be put on the stack."""

  def static_value16(self):
    v = self.static_value()
    if v is None: return None
    return v & 0xffff

class Number(LiteralValue):

  def __init__(self, value, base = 10):
    self.value = value
    self.base = base

  def __repr__(self):
    if self.value < 0: s = '-'
    else: s = ''
    if self.base == 10: return s + str(abs(self.value))
    else: return s + hex(abs(self.value))

  def deep_references(self, stack):
    return stack

  def static_value(self): return self.value

  def __add__(self, i):
    return Number(self.value + i, self.base)

dst_w = Number(0)
dst_f = Number(1)
access = Number(0)
no_access = Number(1)
no_fast = Number(0)
fast = Number(1)

def in_access_bank(addr):
  """Check whether an address can be accessed through the access bank."""
  addr = addr.static_value()
  return addr is not None and addr <= 0x5f or(addr >= 0xf60 and addr <= 0xfff)

def in_bank_1(addr):
  """Check whether an address can be accessed in bank 1 using BSR."""
  addr = addr.static_value()
  return addr is not None and(addr & 0xff00 == 0x0100)

def short_addr(addr):
  """Check whether an address can be accessed using a short reference, with
  or without an access bank."""
  return in_access_bank(addr) or in_bank_1(addr)

def access_bit(addr):
  """Return the right access bit depending on whether the address is present
  in the access bank or not."""
  if in_access_bank(addr): return access
  else: return no_access

def ram_addr(addr):
  """Check whether the access designates a RAM address."""
  addr = addr.static_value()
  return addr is not None and(addr & 0xf000 == 0x0000)

def eeprom_addr(addr):
  """Check whether the access designates an EEPROM address."""
  addr = addr.static_value()
  return addr is not None and(addr & 0xf000 == 0x1000)

def flash_addr(addr):
  """Check whether the access designates an flash address."""
  addr = addr.static_value()
  return addr is not None and(addr & 0x8000 == 0x8000)

def is_static_push(opcode):
  return opcode[0] == 'OP_PUSH' and opcode[1][0].static_value() is not None

class Named:

  def __init__(self, name, compile = True):
    self.name = name
    if compile: compiler.start_compilation(self)
    else: compiler.enter_object(self)
    self.immediate = True
    self.references = []
    self.section = 'undefined'
    self.definition = compiler.current_location()
    self.inlined = False
    self.inw = False
    self.outw = False
    self.outz = False
    self.from_source = True
    self.referenced_by = 0
    self.not_inlinable = False

  def reset_referenced_by(self):
    self.referenced_by = 0

  def should_inline(self):
    """Return True if this word should be inlined."""
    return False

  def can_inline(self):
    """Return True if this word can be inlined."""
    return False
  
  def deep_references(self, stack):
    self.referenced_by += 1
    if self in stack: return
    stack.append(self)
    for i in self.references: i.deep_references(stack)
    self.prepare()
    return stack

  def enter(self):
    compiler.enter()

  def __repr__(self):
    name = self.name
    if name not in Compiler.all_opcodes:
      for k,v in [('?', 'QM'), ('!', 'EX'), ('@', 'AT'), ('+', 'PL'),
                 ('-', 'MI'), ('*', 'ST'), ('/', 'SL'), ('=', 'EQ'),
                 ('<', 'LT'), ('>', 'GT'), ('$', 'DO'), ('.', 'DT'),
                 ('"', 'QU'), ("'", 'QT'), (':', 'CL'), (';', 'SC'),
                 ('(', 'OP'), (')', 'CP'), ('%', 'PC')]:
        name = string.replace(name, k, "_%s_" % v)
    if self.occurrence: suffix = '__%d' % self.occurrence
    else: suffix = ''
    if name[0] in string.digits: prefix = '_'
    else: prefix = ''
    name = prefix + name + suffix
    if name in Compiler.gpasm_directives: name = '_' + name
    return name

  def unsubstituted(self): return `self`

  def refers_to(self, object):
    if object is None or isinstance(object, (Number, str, unicode)): return
    if self != object: self.references.append(object)

  def output_header(self, outfd):
    outfd.write('; %s: defined at %s\n' %(self.name, self.definition))

  def prepare(self): pass

class Binary(LiteralValue):

  def __init__(self, op, v1, v2):
    self.op = op
    self.v1 = v1
    self.v2 = v2

  def __repr__(self):
    return '(%s%s%s)' %(self.v1, self.op, self.v2)

  def deep_references(self, stack):
    self.v1.deep_references(stack)
    self.v2.deep_references(stack)
    return stack

  def static_value(self):
    a1, a2 = self.v1.static_value(), self.v2.static_value()
    if a1 is not None and a2 is not None: return self.compute(a1, a2)
    else: return None    

class Add(Binary):

  def __init__(self, v1, v2): Binary.__init__(self, '+', v1, v2)

  def compute(self, a1, a2): return a1+a2

class Sub(Binary):

  def __init__(self, v1, v2): Binary.__init__(self, '-', v1, v2)

  def compute(self, a1, a2): return a1-a2

class LeftShift(Binary):

  def __init__(self, v1, v2): Binary.__init__(self, '<<', v1, v2)

  def compute(self, a1, a2): return a1 << a2

class Unary(LiteralValue):

  def __init__(self, value):
    self.value = value

  def static_value(self):
    a = self.value.static_value()
    if a is not None: return self.compute(a)

  def deep_references(self, stack): return self.value.deep_references(stack)

class Low(Unary):

  def __repr__(self):
    return 'LOW(%s)' % self.value

  def compute(self, a): return a & 0xff

class High(Unary):

  def __repr__(self):
    return 'HIGH(%s)' % self.value

  def compute(self, a): return a >> 8

def low(x):
  v = x.static_value()
  if v is not None and v >= 0 and v <= 0xff: return x
  else: return Low(x)

def high(x):
  v = x.static_value()
  if v is not None and v >= 0 and v <= 0xff: return Number(0)
  else: return High(x)

class Negated(Unary):

  def __repr__(self):
    return '(-%s)' % self.value

  def compute(self, a): return -a

class Primitive(Named): pass

class AsmInstruction(Primitive):

  def run(self):
    compiler.add_instruction(self.name, [])

class NamedReference(Named):
  """Label with an implicit or explicit name."""

  def __init__(self, name = None):
    if name == None: label_name = '_lbl_'
    else: label_name = name
    Named.__init__(self, label_name, compile = False)
    self.from_source = name is not None

  def run(self):
    compiler.ct_push(self)

  def static_value(self):
    return None

class Label(NamedReference): pass

class FlashData(NamedReference):

  def __init__(self, data, original_data):
    NamedReference.__init__(self, '_data_')
    self.data = data
    self.original_data = original_data
    self.section = 'static data'
    self.from_source = False

  def output_header(self, outfd):
    outfd.write('; defined at %s as:\n; %s\n' %
                (self.definition, self.original_data))

  def output(self, outfd):
    outfd.write('%s' % self)
    count = -1
    for i in self.data:
      count += 1
      if count % 8 == 0:
        count = 0
        outfd.write('\n\tdb ')
      if count != 0: outfd.write(',')
      outfd.write('%d' % i)
    outfd.write('\n')

class MakeLabel(Primitive):

  def run(self):
    label = Label(compiler.parse_word())
    compiler.add_instruction('LABEL', [label])

class Forward(Label):
  """Forward declaration."""

  def __init__(self, name):
    Label.__init__(self, name)
    compiler.start_compilation(self)

  def run(self):
    if compiler.state:
      compiler.add_call(self)
    else:
      compiler.ct_push(self)

  def output(self, _):
    compiler.error('%s(defined at %s) needs to be overloaded' %
                   (self.name, self.definition))

class MakeForward(Primitive):

  def run(self):
    Forward(compiler.parse_word())

class IntrProtect(Primitive):

  def run(self):
    compiler.add_instruction('OP_INTR_PROTECT', [])

class IntrUnprotect(Primitive):

  def run(self):
    compiler.add_instruction('OP_INTR_UNPROTECT', [])

class Char(Primitive):

  def run(self):
    char = Number(ord(compiler.parse_word()[0]))
    if compiler.state: compiler.push(char)
    else: compiler.ct_push(char)

class Begin(Primitive):
  """Push a counter and a backward label onto the stack."""

  def run(self):
    compiler.ct_push(0)
    label = Label()
    compiler.ct_push(label)
    compiler.add_instruction('LABEL', [label])

class Again(Primitive):
  """Jump back to a label from the stack."""

  def run(self, do_not_pop_counter = False):
    label = compiler.ct_pop()
    compiler.add_instruction('bra', [label])
    if not do_not_pop_counter: assert(compiler.ct_pop() == 0)

class OpeningBracket(Primitive):
  """Escape to interpretation mode."""

  def run(self):
    compiler.state = 0

class ClosingBracket(Primitive):
  """Return to compilation mode."""

  def run(self):
    compiler.state = 1

class Literal(Primitive):
  """Pop a literal from the stack and generate code to push it at run time."""

  def run(self):
    compiler.push(compiler.ct_pop())

class ToW(Primitive):
  """Move the top of stack into W."""

  def run(self, warn = True):
    name, params = compiler.last_instruction()
    if name == 'OP_PUSH':
      compiler.rewind()
      value = params[0]
      s = value.static_value()
      if warn and s is not None and(s < -128 or s > 255):
        compiler.warning('value will not fit in W register')
      compiler.add_instruction('movlw', [low(value)])
    elif name in ['OP_FETCH', 'OP_CFETCH'] and ram_addr(params[0]):
      compiler.rewind()
      addr = params[0]
      if warn and name == 'OP_FETCH':
        compiler.warning('value may not fit in W register')
      if short_addr(addr):
        compiler.add_instruction('movf',
                                       [addr, dst_w, access_bit(addr)])
      else:
        compiler.add_instruction('movff', [addr, compiler['WREG']])
    elif name == 'OP_DUP':
      compiler.rewind()
      compiler.add_instruction('movf', [compiler['INDF0'], dst_w, access])
    elif name == 'OP_PUSH_W':
      compiler.rewind()
    else:
      compiler.add_instruction('OP_POP_W', [])

class Dup(Primitive):
  """Duplicate top of stack"""

  def run(self):
    name, params = compiler.last_instruction()
    if name in ['OP_PUSH', 'OP_PUSH_W']:
      compiler.add_instruction(name, params)
    elif name in ['OP_CFETCH']:
      compiler.rewind()
      addr = params[0]
      compiler.add_instruction('movf', [addr, dst_w, access_bit(addr)])
      compiler.add_instruction('OP_PUSH_W')
      compiler.add_instruction('OP_PUSH_W')
    else:
      compiler.add_instruction('OP_DUP')

class Drop(ToW):
  """Drop the top of stack"""
  
  def run(self):
    name, params = compiler.last_instruction()
    if name in ['OP_PUSH', 'OP_DUP']:
      compiler.rewind()
    elif name == 'OP_FETCH' and ram_addr(params[0]) and \
         params[0].static_value() < 0xf60:
      # Regular memory read, can be safely removed
      compiler.rewind()
    else:
      ToW.run(self, warn = False)

class FromW(Primitive):
  """Move W onto the top of stack."""

  def run(self):
    name, params = compiler.last_instruction()
    if name == 'OP_POP_W':
      compiler.rewind()
    elif name == 'movf' and params[1] == dst_w:
      compiler.rewind()
      compiler.push(params[0])
      compiler['c@'].run()
    else:
      compiler.add_instruction('OP_PUSH_W', [])

class LAnd(Primitive):
  """Logical and."""

  def run(self):
    name, params = compiler.last_instruction()
    if name == 'OP_PUSH' and octet(params[0].static_value()):
      compiler.rewind()
      compiler['>w'].run()
      compiler.add_instruction('andlw', params)
      compiler['w>'].run()
    else:
      compiler['op_and'].run()

class CFor(Primitive):
  """Simple loop with a one byte index."""

  def run(self):
    name, params = compiler.last_instruction()
    label_uncfor = Label()
    label_noloop = Label()
    compiler.ct_push(label_noloop)
    compiler.ct_push(label_uncfor)
    if name in ['OP_FETCH', 'OP_CFETCH'] and ram_addr(params[0]):
      compiler.rewind()
      addr = params[0]
      compiler.add_instruction('movff', [addr, compiler['PREINC2']])
      if name == 'OP_FETCH':
        compiler.warning('loop index may be larger than one byte')
      compiler.add_instruction('movf', [compiler['PREINC2'], dst_w, access])
      compiler.add_instruction('bz', [label_uncfor])
    else:
      bound_checks = True
      if name == 'OP_PUSH':
        value = params[0].static_value()
        if value == 0:
          compiler.rewind()
          compiler.warning('empty loop will not execute')
          compiler.add_instruction('bra', [label_noloop])
        elif value < 0 or value > 255:
          compiler.warning('loop limit does not fit in a byte')
        elif value is not None:
          bound_checks = False
      compiler['>w'].run()
      compiler.add_instruction('movwf',
                                     [compiler['PREINC2'], access])
      if bound_checks:
        name, params = compiler.before_last_instruction()
        if name != 'OP_POP_W':
          compiler.add_instruction('iorlw', [Number(0)])
        compiler.add_instruction('bz', [label_uncfor])
    compiler['begin'].run()

class Ahead(Primitive):
  """Create and use a forward label."""

  def run(self):
    label = Label()
    compiler.ct_push(label)
    compiler.add_instruction('bra', [label])

class Then(Primitive):
  """Resolve a forward label from the stack."""

  def run(self):
    label = compiler.ct_pop()
    compiler.add_instruction('LABEL', [label])

class Repeat(Primitive):
  """Jump back to a label from the stack and declare forward label."""

  def run(self):
    compiler['again'].run(do_not_pop_counter = True)
    counter = compiler.ct_pop()
    for _ in range(counter): compiler['then'].run()

class If(Primitive):
  """Compile a test."""

  def run(self, invert = False):
    name, params = compiler.last_instruction()
    if name == 'OP_NORMALIZE':
      compiler.rewind()
      return self.run(invert)
    if name == 'OP_0=':
      compiler.rewind()
      return self.run(not invert)
    if name == 'OP_BIT_SET?':
      compiler.rewind()
      value = params[0]
      bit = params[1]
      acc = params[2]
      invert = not invert
    elif name == 'OP_BIT_CLR?':
      compiler.rewind()
      value = params[0]
      bit = params[1]
      acc = params[2]
    else:
      if name != 'MARKER_ZSET':
        compiler.add_instruction('movf', [compiler['POSTDEC0'],
                                           dst_w, access])
        compiler.add_instruction('iorwf', [compiler['POSTDEC0'],
                                            dst_w, access])
      value, bit = compiler['Z']
      acc = access
    if invert: ins = 'btfss'
    else: ins = 'btfsc'
    compiler.add_instruction(ins, [value, bit, acc])
    compiler['ahead'].run()

class SwitchW(Primitive):
  """Simple switch statement"""

  # Structure of the switch statement on the compile stack is:
  #   - next label or None for the first case
  #   - switch end label
  #   - xored value

  def run(self):
    compiler.ct_push(None)
    compiler.ct_push(Label())
    compiler.ct_push(0)

class CaseW(Primitive):

  def run(self):
    name, params = compiler.last_instruction()
    if name != 'OP_PUSH':
      raise Compiler.FATAL_ERROR, \
            "%s: casew must be used with a constant" % \
            compiler.current_location
    compiler.rewind()
    xored = compiler.ct_pop()
    label = compiler.ct_pop()
    nlabel = compiler.ct_pop()
    if nlabel is not None:
      compiler.add_instruction('bra', [label])
      compiler.add_instruction('LABEL', [nlabel])
    xored ^= params[0].static_value()
    compiler.add_instruction('xorlw', [Number(xored)])
    value, bit = compiler['Z']
    compiler.add_instruction('btfss', [value, bit, access])
    nlabel = Label()
    compiler.add_instruction('bra', [nlabel])
    compiler.ct_push(nlabel)
    compiler.ct_push(label)
    compiler.ct_push(params[0].static_value())

class EndSwitchW(Primitive):

  def run(self):
    xored = compiler.ct_pop()
    label = compiler.ct_pop()
    nlabel = compiler.ct_pop()
    compiler.add_instruction('LABEL', [nlabel])
    compiler.add_instruction('LABEL', [label])

class AddressOf(Primitive):
  """Implement the ['] word."""

  def run(self):
    compiler.push(compiler.find(compiler.parse_word()))

class Jump(Primitive):
  """Implement the execute word."""

  def run(self):
    compiler.add_instruction ('clrf', [compiler['PCLATU'], access])
    compiler.push(compiler['PCL'])
    compiler['!'].run()

class While(Primitive):
  """Implement the while word."""

  def run(self, is_until = False):
    flabel = compiler.ct_pop()
    counter = compiler.ct_pop()
    compiler['if'].run(invert = is_until)
    compiler.ct_push(counter+1)
    compiler.ct_push(flabel)

class Until(Primitive):
  """Implement the until word."""

  def run(self):
    compiler['while'].run(is_until = True)
    compiler['repeat'].run()

class CNext(Primitive):
  """cnext corresponding to a preceding cfor."""

  def run(self):
    compiler.add_instruction('decfsz',
                                   [compiler['INDF2'], dst_f, access])
    compiler['again'].run()
    label = compiler.ct_pop()
    compiler.add_instruction('LABEL', [label])
    compiler.add_instruction('movf',
                                   [compiler['POSTDEC2'], dst_f, access])
    label = compiler.ct_pop()
    compiler.add_instruction('LABEL', [label])

class Else(Primitive):
  """Create and use a forward label the resolve a backward one."""

  def run(self):
    compiler['ahead'].run()
    compiler.ct_swap()
    compiler['then'].run()

class Normalize(Primitive):
  """Mark an entry on the stack as being a boolean(-1 or 0)."""

  def run(self):
    name, params = compiler.last_instruction()
    if name != 'OP_NORMALIZE': compiler.add_instruction('OP_NORMALIZE', [])

class ZeroEqual(Primitive):
  """Invert the boolean on the stack."""

  def run(self):
    name, params = compiler.last_instruction()
    if name == 'OP_0=':
      compiler.rewind()
      compiler['0<>'].run()
      return
    if name == 'OP_NORMALIZE': compiler.rewind()
    compiler.add_instruction('OP_0=', [])    

class BitOp(Primitive):

  def run(self, kind):       # kind can be 'set', 'clear' or 'toggle'
    name, params = compiler.last_instruction()
    if name == 'OP_PUSH':
      # The bit to change is statically known
      compiler.rewind()
      bit = params[0]
      name, params = compiler.last_instruction()
      if kind == 'set': op = 'bsf'
      elif kind == 'clear': op = 'bcf'
      elif kind == 'toggle': op = 'btg'
      if name == 'OP_PUSH' and short_addr(params[0]):
        # The address can also be access directly
        compiler.rewind()
        addr = params[0]
        compiler.add_instruction(op, [addr, bit, access_bit(params[0])])
      else:
        # Latch through FSR1
        compiler.pop_to_fsr(1)
        compiler.add_instruction(op, [compiler['INDF1'], bit,
                                            access])
    else:
      # Resort to a library function
      if kind == 'set':
        compiler['op_bit_set'].run()
      elif kind == 'clear':
        compiler['op_bit_clr'].run()
      else:
        compiler['op_bit_toggle'].run()

class BitSet(BitOp):
  """Set a bit."""

  def run(self): BitOp.run(self, kind = 'set')

class BitClr(BitOp):
  """Clear a bit."""

  def run(self): BitOp.run(self, kind = 'clear')

class BitToggle(BitOp):
  "Toggle a bit."""

  def run(self): BitOp.run(self, kind = 'toggle')

class BitTest(Primitive):
  """Perform a bit test."""

  def run(self, invert = False):
    name, params = compiler.last_instruction()
    if name == 'OP_PUSH':
      # The bit to test is statically known
      compiler.rewind()
      bit = params[0]
      name, params = compiler.last_instruction()
      if invert: op = 'OP_BIT_CLR?'
      else: op = 'OP_BIT_SET?'
      if name == 'OP_PUSH' and short_addr(params[0]):
        # The address is also usable as-is
        compiler.rewind()
        addr = params[0]
        compiler.add_instruction(op, [addr, bit, access_bit(addr)])
      else:
        # Use FSR1 to latch the address
        compiler.pop_to_fsr(1)
        compiler.add_instruction(op, [compiler['INDF1'], bit,
                                            access])
    else:
      # Resort to a library function
      if invert: compiler['op_bit_clr_q'].run()
      else: compiler['op_bit_set_q'].run()

class BitSetTest(BitTest): pass

class BitClrTest(BitSetTest):
  """Perform a negated bit test."""

  def run(self): BitSetTest.run(self, invert = True)

class BitMask(Primitive):
  """Compute a bit mask."""

  def run(self):
    name, params = compiler.last_instruction()
    if name == 'OP_PUSH':
      # The bit is known, compute a left shift
      compiler.rewind()
      compiler.push(LeftShift(Number(1), params[0]))
    else:
      # Default primitive
      compiler['op_bit_mask'].run()

class Backslash(Primitive):

  def run(self):
    compiler.input_buffer = ''

class Parenthesis(Primitive):

  def run(self):
    compiler.parse(')')

class Include(Primitive):

  def run(self):
    compiler.include(compiler.parse_word())

class Needs(Primitive):

  def run(self):
    compiler.needs(compiler.parse_word())

class Exit(Primitive):

  def run(self):
    compiler.add_instruction('goto', [compiler.current_object.end_label])

class Plus(Primitive):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if is_static_push((name, params)):
        compiler.rewind()
        v = params[0].static_value16()
        if is_static_push(compiler.last_instruction()):
          res = Add(params[0], compiler.last_instruction()[1][0])
          compiler.rewind()
          compiler.push(res)
        elif v == 0:
          pass
        elif v == 1:
          compiler['op_1+'].run()
        elif v == 0x0100:
          compiler.add_instruction('incf', [compiler['INDF0'], dst_f, access])
        elif v == 0xff00:
          compiler.add_instruction('decf', [compiler['INDF0'], dst_f, access])
        elif v is not None and v & 0xff == 0:
          compiler.add_instruction('movlw', [High(params[0])])
          compiler.add_instruction('addwf',
                                    [compiler['INDF0'], dst_f, access])
        else:
          compiler.add_instruction('movlw', [Low(params[0])])
          compiler.add_instruction('movf', [compiler['POSTDEC0'],
                                             dst_f, access])
          compiler.add_instruction('addwf', [compiler['POSTINC0'],
                                              dst_f, access])
          compiler.add_instruction('movlw', [High(params[0])])
          compiler.add_instruction('addwfc', [compiler['INDF0'],
                                               dst_f, access])
      else:
        compiler['op_plus'].run()
    else:
      x2 = compiler.ct_pop()
      x1 = compiler.ct_pop()
      compiler.ct_push(Add(x1, x2))

class Minus(Primitive):

  def run(self):
    if compiler.state:
      if is_static_push(compiler.last_instruction()) and \
         is_static_push(compiler.before_last_instruction()):
        res = Sub(compiler.before_last_instruction()[1][0],
                       compiler.last_instruction()[1][0])
        compiler.rewind()
        compiler.rewind()
        compiler.push(res)
      elif is_static_push(compiler.last_instruction()):
        value = compiler.last_instruction()[1][0]
        compiler.rewind()
        compiler.push(Negated(value))
        compiler['+'].run()
      else:
        compiler['op_minus'].run()
    else:
      x2 = compiler.ct_pop()
      x1 = compiler.ct_pop()
      compiler.ct_push(Sub(x1, x2))

class OnePlus(Primitive):

  def run(self):
    compiler.push(Number(1))
    compiler['+'].run()

class OneMinus(Primitive):

  def run(self):
    compiler.push(Number(1))
    compiler['-'].run()

class OnePlusStore(Primitive):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if name == 'OP_PUSH' and ram_addr(params[0]):
        # Increment a statically known RAM address
        compiler.rewind()
        addr = params[0]
        compiler.add_instruction('infsnz', [addr, dst_f])
        compiler.add_instruction('incf', [Add(addr, Number(1)), dst_f])
        return
    compiler['dup'].run()
    compiler['@'].run()
    compiler['1+'].run()
    compiler['swap'].run()
    compiler['!'].run()

class CPlusStore(Primitive):

  def run(self, negate = False):
    name, params = compiler.last_instruction()
    if is_static_push((name, params)):
      compiler.rewind()
      addr = params[0]
      name, params = compiler.last_instruction()
      if is_static_push((name, params)):
        value = params[0]
        svalue = value.static_value()
        if negate: svalue = -svalue
        if svalue == 0:
          compiler.rewind()
          return
        elif svalue == 1:
          compiler.rewind()
          compiler.add_instruction('incf', [addr, dst_f, access_bit(addr)])
          return
        elif svalue == -1 or svalue == 255:
          compiler.rewind()
          compiler.add_instruction('decf', [addr, dst_f, access_bit(addr)])
          return
      compiler['>w'].run()
      compiler.add_instruction('addwf', [addr, dst_f, access_bit(addr)])
    elif negate:
      compiler['op_c-!'].run()
    else:
      compiler['op_c+!'].run()

class CMinusStore(CPlusStore):

  def run(self):
    CPlusStore.run(self, negate = True)

class LShift(Primitive):

  def run(self):
    if compiler.state:
      if is_static_push(compiler.last_instruction()) and \
         is_static_push(compiler.before_last_instruction()):
        name, nsteps = compiler.last_instruction()
        compiler.rewind()
        name, operand = compiler.last_instruction()
        compiler.rewind()
        compiler.push(LeftShift(operand[0], nsteps[0]))
      else:
        compiler['cfor'].run()
        compiler['2*'].run()
        compiler['cnext'].run()
    else:
      nsteps = compiler.ct_pop()
      operand = compiler.ct_pop()
      compiler.ct_push(LeftShift(operand, nsteps))

class Equal(Primitive):

  def run(self):
    if is_static_push(compiler.last_instruction()) and \
       compiler.last_instruction()[1][0].static_value() == 0:
      compiler.rewind()
      compiler['0='].run()
    else:
      compiler['op_='].run()

class Colon(Primitive):

  def run(self):
    compiler.state = 1
    name = compiler.parse_word()
    Word(name).end_label = Label()
    
class SemiColon(Primitive):

  def run(self):
    compiler.add_instruction('LABEL', [compiler.current_object.end_label])
    compiler.add_instruction('return', [no_fast])
    compiler.enter()
    compiler.state = 0

class Recurse(Primitive):

  def run(self):
    compiler.add_call(compiler.current_object)

class MakeConstant(Primitive):

  def run(self):
    name = compiler.parse_word()
    Constant(name, compiler.ct_pop())

class Constant(Named, LiteralValue):

  def __init__(self, name, value):
    Named.__init__(self, name)
    self.value = value
    compiler.enter()
    self.immediate = True
    self.refers_to(value)
    self.section = 'constants'

  def output(self, outfd):
    outfd.write("%s equ %s\n" %(self, self.value))

  def run(self):
    if compiler.state:
      compiler.push(self)
    else:
      compiler.ct_push(self)

  def static_value(self):
    if type(self.value) == int: return self.value
    else: return self.value.static_value()

class Bit(Constant):

  def __init__(self, name, value, bit):
    Constant.__init__(self, name, value)
    self.bit = bit
    self.refers_to(bit)

  def run(self):
    if compiler.state:
      compiler.push(self.value)
      compiler.push(self.bit)
    else:
      compiler.ct_push(self.value)
      compiler.ct_push(self.bit)

  def static_value(self):
    raise Compiler.INTERNAL_ERROR

class MakeBit(Primitive):

  def run(self):
    name = compiler.parse_word()
    bit = compiler.ct_pop()
    addr = compiler.ct_pop()
    Bit(name, addr, bit)

class StoreOp(Primitive):

  def write_w(self, addr):
    if short_addr(addr):
      compiler.add_instruction('movwf', [addr, access_bit(addr)])
    else:
      compiler.add_instruction('movff', [compiler['WREG'], addr])

  def write_literal(self, value, addr):
    if short_addr(addr):
      if value.static_value() == 0:
        compiler.add_instruction('clrf', [addr, access_bit(addr)])
      elif value.static_value() == 0xff:
        compiler.add_instruction('setf', [addr, access_bit(addr)])
      else:
        compiler.add_instruction('movlw', [value])
        self.write_w(addr)
    else:
      compiler.add_instruction('movlw', [value])
      compiler.add_instruction('movff', [compiler['WREG'], addr])
        
class Store(StoreOp):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if name == 'OP_PUSH' and ram_addr(params[0]):
        # The store tries to write at a statically known RAM address
        compiler.rewind()
        addr = params[0]
        addr1 = Add(addr, Number(1))
        name, params = compiler.last_instruction()
        if name == 'OP_PUSH':
          # Constant write
          compiler.rewind()
          const = params[0]
          self.write_literal(low(const), addr)
          self.write_literal(high(const), addr1)
        elif name == 'OP_FETCH' and ram_addr(params[0]):
          # Memory move
          compiler.rewind()
          compiler.add_instruction('movff',
                                         [Add(params[0],
                                                   Number(1)),
                                          addr1])
          compiler.add_instruction('movff', [params[0], addr])
        elif name == 'OP_CFETCH' and ram_addr(params[0]):
          # Memory byte move with one byte
          compiler.rewind()
          compiler.add_instruction('movff', [params[0], addr])
          self.write_literal(Number(0), addr1)
        elif name == 'OP_PUSH_W':
          # Write W
          compiler.warning('you may be wanting to use c! here')
          compiler.rewind()
          self.write_w(addr)
          self.write_literal(0, addr1)
        elif name == 'OP_2>1':
          # Get content(LSB then MSB) and store it
          compiler.rewind()
          compiler.add_instruction('movff', [params[0], addr])
          compiler.add_instruction('movff', [params[1], addr1])
        else:
          compiler.tos_to_addr(addr)
      else:
        # Any address and content
        compiler['op_store'].run()

class CStore(StoreOp):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if name == 'OP_PUSH' and ram_addr(params[0]):
        # The store tries to write at a statically known address in RAM
        compiler.rewind()
        addr = params[0]
        name, params = compiler.last_instruction()
        if name == 'OP_PUSH':
          # Constant write
          compiler.rewind()
          const = params[0]
          self.write_literal(low(const), addr)
        elif name in ['OP_FETCH', 'OP_CFETCH'] and ram_addr(params[0]):
          # Memory move
          compiler.rewind()
          if name == 'OP_FETCH':
            compiler.warning('target may not be large enough to '
                                   'store entire result')
          compiler.add_instruction('movff', [params[0], addr])
        elif name == 'OP_PUSH_W':
          # Write W
          compiler.rewind()
          self.write_w(addr)
        else:
          compiler.tos_to_addr_byte(addr)
      else:
        # Any address and content
        compiler['op_cstore'].run()

class Allot(Primitive):

  def run(self):
    compiler.allot(compiler.ct_pop().static_value())

class Variable(Constant):

  def __init__(self, name, size, initial_value = None, zone = 'RAM'):
    if zone == 'RAM':
      self.addr = compiler.here
      compiler.allot(size)
    elif zone == 'EEPROM':
      self.addr = compiler.eehere
      compiler.eehere += size
    else:
      raise Compiler.UNIMPLEMENTED
    Constant.__init__(self, name, Number(self.addr, 16))
    self.section = 'memory'
    if compiler.initialize_variables and size > 0 and \
           initial_value != 'NO_INIT':
      compiler.push_init_runtime()
      if not initial_value: initial_value = Number(0)
      compiler.push(initial_value)
      compiler.push(self)
      if size == 1:
        compiler['c!'].run()
      else:
        compiler['!'].run()
      compiler.pop_object()

class Create(Primitive):

  def run(self):
    name = compiler.parse_word()
    Variable(name, 0)

class MakeVariable(Primitive):

  def run(self):
    name = compiler.parse_word()
    Variable(name, 2)

class Value(Variable):

  def run(self):
    compiler.push(self)
    compiler['@'].run()

class MakeCVariable(Primitive):

  def run(self):
    name = compiler.parse_word()
    Variable(name, 1)

class MakeEEVariable(Primitive):

  def run(self):
    name = compiler.parse_word()
    Variable(name, 2, 'NO_INIT', 'EEPROM')

class MakeEECVariable(Primitive):

  def run(self):
    name = compiler.parse_word()
    Variable(name, 1, 'NO_INIT', 'EEPROM')

class MakeValue(Primitive):

  def run(self):
    value = compiler.ct_pop()
    name = compiler.parse_word()
    Value(name, 2, value)

class Comma(Primitive):

  count = 0

  def next_count(self):
    r = Comma.count
    Comma.count += 1
    return r

  def run(self, size = 2):
    value = compiler.ct_pop()
    name = '_unnamed_%d' % self.next_count()
    Variable(name, size, value)

class CComma(Comma):

  def run(self):
    Comma.run(self, size = 1)

class Fetch(Primitive):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if name == 'OP_PUSH' and ram_addr(params[0]):
        # Statically known address
        compiler.rewind()
        compiler.add_instruction('OP_FETCH', params)
      else:
        compiler.add_instruction('OP_FETCH_TOS', [])
    else:
      raise Compiler.FATAL_ERROR, \
            "%s: cannot fetch while interpreting" % compiler.current_location

class CFetch(Primitive):

  def run(self):
    if compiler.state:
      name, params = compiler.last_instruction()
      if name == 'OP_PUSH' and ram_addr(params[0]):
        # Statically known address
        compiler.rewind()
        compiler.add_instruction('OP_CFETCH', params)
      else:
        compiler.add_instruction('OP_CFETCH_TOS', [])
    else:
      raise Compiler.FATAL_ERROR, \
            "%s: cannot fetch byte while interpreting" % \
            compiler.current_location

class To(Primitive):

  def run(self):
    name = compiler.parse_word()
    compiler.push(compiler.find(name))
    compiler['!'].run()

class TwoToOne(Primitive):
  """Combine a LSB and a MSB into one cell."""

  def run(self):
    name, params = compiler.last_instruction()
    if name in ['OP_FETCH', 'OP_CFETCH'] and ram_addr(params[0]):
      compiler.rewind()
      msb = params[0]
      name, params = compiler.last_instruction()
      if name in ['OP_FETCH', 'OP_CFETCH'] and ram_addr(params[0]):
        compiler.rewind()
        lsb = params[0]
        compiler.add_instruction('OP_2>1', [lsb, msb])
      else:
        # Replace msb on stack
        compiler.add_instruction('movff', [msb, compiler['INDF0']])
    else:
      # Resort to library routine
      compiler['op_2>1'].run()

class Inline(Primitive):
  """Mark the latest defined word as inlined."""

  def run(self):
    compiler.current_object.inlined = True

class NoInline(Primitive):
  """Mark the latest defined word as not inlinable."""

  def run(self):
    compiler.current_object.not_inlineable = True

class LowInterrupt(Primitive):
  """Mark the latest defined word as being the low-level interrupt."""

  def run(self):
    compiler.check_interrupts()
    compiler.low_interrupt = compiler.current_object
    compiler.rewind()
    compiler.add_instruction('retfie', [no_fast])

class HighInterrupt(Primitive):
  """Mark the latest defined word as being the low-level interrupt."""

  def run(self):
    compiler.check_interrupts()
    compiler.high_interrupt = compiler.current_object
    compiler.rewind()
    compiler.add_instruction('retfie', [no_fast])

class Fast(Primitive):
  """Mark the latest word as returning fast(return or retfie)."""

  def run(self):
    name, params = compiler.last_instruction()
    if params == [no_fast]:
      compiler.rewind()
      compiler.add_instruction(name, [fast])

class InW(Primitive):
  """Mark the latest defined word as getting its argument in W."""

  def run(self):
    compiler.current_object.inw = True

class OutW(Primitive):
  """Mark the latest defined word as getting its argument in W."""

  def run(self):
    compiler.current_object.outw = True

class OutZ(Primitive):
  """Mark the latest defined word as returning its truth value in Z."""

  def run(self):
    compiler.current_object.outz = True

def is_internal_jump(opcode):
  return opcode[0] in ['goto', 'bra'] and isinstance(opcode[1][0], Label)

def is_external_jump(opcode):
  if opcode[0] in ['return', 'retfie']: return True
  return opcode[0] == 'goto' and isinstance(opcode[1][0], (Label, Word))

def is_jump(opcode):
  return is_internal_jump(opcode) or is_external_jump(opcode)

def last_goto(x):
  """Check whether the last instruction of x is a real goto to somewhere
  else."""
  if len(x.opcodes) == 0: return False
  if len(x.opcodes) == 1: return is_jump(x.opcodes[-1])
  return is_jump(x.opcodes[-1]) and \
         x.opcodes[-2][0] not in ['btfss', 'btfsc', 'decfsz', 'incfsz',
                                  'infsnz', 'dcfsnz', 'tstfsz']

class PICIns(Primitive):

  prefix = False

  def __init__(self, name, format):
    Primitive.__init__(self, name)
    self.format = format

  def run(self):
    if PICIns.prefix:
      compiler.interpret(compiler.input_buffer)
      compiler.input_buffer = ''
    args = []
    for i in self.format:
      if i == 'l': args.append(compiler.ct_pop())
    args.reverse()
    if 'f' in self.format:
      if PICIns.f is None:
        compiler.warning('implicit destination F assumed')
        PICIns.f = 1
      if PICIns.f: args.append(dst_f)
      else: args.append(dst_w)
    elif PICIns.f is not None:
      compiler.error('bogus destination specification')
    if 'a' in self.format:
      if PICIns.a is None:
        compiler.warning('implicit access bank assumed')
        PICIns.a = 0
      if PICIns.a: args.append(no_access)
      else: args.append(access)
    elif PICIns.a is not None:
      compiler.error('bogus access bank specification')
    if 's' in self.format:
      if PICIns.s: args.append(fast)
      else: args.append(no_fast)
    compiler.add_instruction(`self`, args)
    PICIns_reset()

def PICIns_reset():
  PICIns.f = None
  PICIns.a = None
  PICIns.s = None

PICIns_reset()

class Code(Primitive):
  """Inline assembly."""

  def run(self):
    name = compiler.parse_word()
    Word(name)
    compiler.state = 0
    PICIns.ct_depth = len(compiler.data_stack)

class Python(Primitive):
  """Inline Python extensions to the core compiler."""

  def run(self):
    """Include everything up to ;python in the default context."""
    lines = []
    while True:
      compiler.refill()
      words = compiler.input_buffer.split('#',1)[0].split()
      if words and words[0].strip() == ';python': break
      lines.append(compiler.input_buffer)
    compiler.input_buffer = ''
    exec string.join(lines, '\n') in globals()

class SemiColonCode(Primitive):
  """End code definition."""

  def run(self):
    compiler.enter()
    if len(compiler.data_stack) != PICIns.ct_depth:
      compiler.warning('wrong count on items on compiler '
                             'stack(%d instead of %d)' %
                            (len(compiler.data_stack), PICIns.ct_depth))

class CommaA(Primitive):
  def run(self): PICIns.a = 0
class Comma0(Primitive):
  def run(self): PICIns.a = 0
class Comma1(Primitive):
  def run(self): PICIns.a = 1
class CommaW(Primitive):
  def run(self): PICIns.f = 0
class CommaF(Primitive):
  def run(self): PICIns.f = 1
class CommaS(Primitive):
  def run(self): PICIns.s = 1
class Prefix(Primitive):
  def run(self): PICIns.prefix = True
class Postfix(Primitive):
  def run(self): PICIns.prefix = False

class Word(Named, Literal):

  def __init__(self, name):
    Named.__init__(self, name)
    self.opcodes = []
    self.section = 'code'
    self.definition = compiler.current_location()
    self.prepared = None
    self.substitute = None
    self.immediate = False
    self.nrefs = 0                  # Number of references to this word

  def __repr__(self):
    if self.substitute:
      return self.substitute.__repr__()
    else: return Named.__repr__(self)

  def real_instance(self):
    """Follow substitutions."""
    if self.substitute: return self.substitute.real_instance()
    return self

  def unsubstituted(self):
    if self.substitute: return '; %s' % self.name
    else: return `self`

  def can_inline(self):
    if self.not_inlinable: return False
    if self in [compiler.low_interrupt, compiler.high_interrupt]:
      return False
    for n, p in self.opcodes[:-1]:
      if is_external_jump((n, p)): return False
    return True

  def should_inline(self):
    if self.inlined or not self.from_source or \
           self == compiler.find(compiler.main):
      return None
    actual_length = len(self.opcodes) + self.referenced_by
    projected_length = len(self.opcodes) * self.referenced_by
    return actual_length >= projected_length

  def add_instruction(self, instruction, params):
    self.opcodes.append((instruction, params))
    for p in params:
      self.refers_to(p)

  def run(self):
    compiler.add_call(self)

  def prepare(self):
    if self.prepared: return
    self.prepared = True
    self.expand()
    self.remove_markers()
    self.optimize()

  def dump(self, msg = ''):
    if msg: msg = '(%s)' % msg
    stderror("Dumping content of %s%s:" %(self.name, msg))
    for o in self.opcodes:
      stderror("   %s %s" %(o[0], string.join([`x` for x in o[1]], ",")))

  def remove_markers(self):
    self.opcodes = [o for o in self.opcodes if o[0][:7] != 'MARKER_']

  def optimize(self):
    """Apply optimizations at the opcode level."""
    while True:
      old_opcodes = self.opcodes[:]
      self.optimize_tail_calls()
      self.optimize_chained_calls()
      self.optimize_dead_labels()
      self.optimize_dead_code()
      self.optimize_small_gotos()
      self.optimize_short_conditions()
      self.optimize_useless_gotos()
      self.optimize_duplicate_labels()
      self.optimize_single_goto()
      if self.opcodes == old_opcodes: break

  def optimize_tail_calls(self):
    new = []
    o = 0
    while o < len(self.opcodes):
      if self.opcodes[o][0] == 'call' and \
             self.opcodes[o][1][1] == no_fast and \
             self.opcodes[o+1][0] == 'return' and \
             self.opcodes[o+1][1][0] == no_fast:
        # Replace call by goto and skip over return
        target = self.opcodes[o][1][0]
        if isinstance(target, Label):
          new.append(('bra', [target]))
        else:
          new.append(('goto', [target]))
        o += 1
      else:
        new.append(self.opcodes[o])
      o += 1
    self.opcodes = new

  def instruction_at_label(self, label):
    """Find the next non-label instruction following a label."""
    for o in range(len(self.opcodes)):
      if self.opcodes[o] ==('LABEL', [label]):
        for i in range(o+1, len(self.opcodes)):
          if self.opcodes[i][0] != 'LABEL': return self.opcodes[i]
    return None

  def optimize_chained_calls(self):
    for o in range(len(self.opcodes)):
      if self.opcodes[o][0] in ['goto', 'bra']:
        target = self.opcodes[o][1][0]
        ins = self.instruction_at_label(target)
        if ins and ins[0] in ['goto', 'bra', 'return', 'retfie', 'reset']:
          self.opcodes[o] = ins

  def optimize_dead_labels(self):
    new = []
    for o in self.opcodes:
      if o[0] == 'LABEL':
        label = o[1][0]
        used = False
        for i in self.opcodes:
          if i != o and i[1] and i[1][0] == label: used = True
        if used: new.append(o)
      else:
        new.append(o)
    self.opcodes = new

  def optimize_dead_code(self):
    new = []
    dead = False
    o = 0
    while o < len(self.opcodes):
      if self.opcodes[o][0] == 'LABEL':
        dead = False
        new.append(self.opcodes[o])
      elif not dead and \
               self.opcodes[o][0] in ['btfss', 'btfsc', 'decfsz', 'dcfsnz',
                                      'incfsz', 'infsnz',
                                      'tstfsz']:
        new.append(self.opcodes[o])
        o += 1
        new.append(self.opcodes[o])
      elif not dead and self.opcodes[o][0] in ['goto', 'bra',
                                               'return', 'retfie', 'reset']:
        new.append(self.opcodes[o])
        dead = True
      elif not dead and self.opcodes[o][0] == 'movwf' and \
               self.opcodes[o][1][0].static_value() == \
               compiler['PCL'].static_value() and \
               self.opcodes[o][1][1] == access:
          new.append(self.opcodes[o])
          dead = True
      elif not dead:
        new.append(self.opcodes[o])
      o += 1
    self.opcodes = new

  conditions = {'btfss' : 'btfsc',
                'btfsc' : 'btfss',
                'decfsz': 'dcfsnz',
                'incfsz': 'infsnz',
                'infsnz': 'incfsz',
                'dcfsnz': 'decfsz'}

  def optimize_small_gotos(self):
    """Invert conditional jump over tests if the following opcode jumps
    to a local label while the alternative is a single jump instruction
    to another word or a return. Also, handle the case where we jump
    over one single instruction."""
    new = []
    o = 0
    while o < len(self.opcodes):
      if self.opcodes[o][0] in Word.conditions and \
             is_internal_jump(self.opcodes[o+1]):
        if is_external_jump(self.opcodes[o+2]):
          new.append((Word.conditions[self.opcodes[o][0]],
                       self.opcodes[o][1]))
          new.append(self.opcodes[o+2])
          new.append(self.opcodes[o+1])
          o += 3
          continue
        elif o+3 < len(self.opcodes) and \
             self.opcodes[o+3] ==('LABEL', [self.opcodes[o+1][1][0]]):
          new.append((Word.conditions[self.opcodes[o][0]],
                       self.opcodes[o][1]))
          new.append(self.opcodes[o+2])
          o += 3
          continue
      new.append(self.opcodes[o])
      o += 1
    self.opcodes = new

  def optimize_short_conditions(self):
    """Use short conditions bc, bnc, bz and bnz if a bit-test is followed
    by a local jump and one of the Z or C bit is tested. Also, if such
    a test jumps over a single external goto, rewrite it using an explicit
    bit-test."""
    new = []
    short_conditions = {make_tuple('btfss', compiler['Z'] + [access]): 'bnz',
                        make_tuple('btfsc', compiler['Z'] + [access]): 'bz',
                        make_tuple('btfss', compiler['C'] + [access]): 'bnc',
                        make_tuple('btfsc', compiler['C'] + [access]): 'bc'}
    o = 0
    while o < len(self.opcodes):
      t = make_tuple(self.opcodes[o][0], self.opcodes[o][1])
      if t in short_conditions and \
         is_internal_jump(self.opcodes[o+1]):
        new.append((short_conditions[t], [self.opcodes[o+1][1][0]]))
        o += 1
      elif t in short_conditions and \
           o+2 < len(self.opcodes) and \
           is_internal_jump(self.opcodes[o+2]):
        reverse =(Word.conditions[t[0]], t[1])
        new.append((short_conditions[reverse], [self.opcodes[o+2][1][0]]))
        new.append(self.opcodes[o+1])
        o += 2
      elif self.opcodes[o][0] in short_conditions.values() and \
               o+2 < len(self.opcodes) and \
               is_external_jump(self.opcodes[o+1]) and \
               self.opcodes[o+2][0] == 'LABEL' and \
               self.opcodes[o][1][0] == self.opcodes[o+2][1][0]:
        for(n, a), v in short_conditions.items():
          if v == self.opcodes[o][0]:
            new.append((Word.conditions[n], list(a)))
            break
        else:
          raise compiler.INTERNAL_ERROR
        new.append(self.opcodes[o+1])
        o += 1
      else:
        new.append(self.opcodes[o])
      o+= 1
    self.opcodes = new

  def optimize_useless_gotos(self):
    """Remove a goto to a label just after."""
    # Note: we cannot have a useless goto after a conditional test
    # by construction.
    new = []
    for o in range(len(self.opcodes)):
      if self.opcodes[o][0] in ['goto', 'bra']:
        useless = True
        target = self.opcodes[o][1][0]
        for i in range(o+1, len(self.opcodes)):
          if self.opcodes[i][0] != 'LABEL':
            useless = False
            break
          if self.opcodes[i][1][0] == target:
            break
        else:
          # No more instructions
          useless = False
        if not useless: new.append(self.opcodes[o])
      else:
        new.append(self.opcodes[o])
      o += 1
    self.opcodes = new

  def replace_label(self, source, target):
    for i in range(len(self.opcodes)):
      if self.opcodes[i] ==('goto', [source]):
        self.opcodes[i] =('goto', [target])
      elif self.opcodes[i] ==('bra', [source]):
        self.opcodes[i] =('bra', [target])

  def optimize_duplicate_labels(self):
    """If two labels follow each other, use the first one in place of
    the second one to ease reading by a human."""
    # If first instruction is a label, dismiss it
    if self.opcodes[0][0] == 'LABEL':
      self.replace_label(self.opcodes[0][1][0], self)
    # Do other lines
    for o in range(len(self.opcodes) - 1):
      if self.opcodes[o][0] == 'LABEL' and \
         self.opcodes[o+1][0] == 'LABEL':
        source = self.opcodes[o+1][1][0]
        target = self.opcodes[o][1][0]
        self.replace_label(source, target)

  def optimize_single_goto(self):
    """If the word is a single goto to another word, replace invocations
    by invocations to this word."""
    if len(self.opcodes) == 1 and self.opcodes[0][0] in ['goto', 'bra']:
      self.substitute = self.opcodes[0][1][0]
      self.opcodes[0] =('COMMENT',
                         ['replaced by equivalent %s' %
                          self.substitute])
                           
  def output(self, outfd):
    outfd.write('%s\n' % self.unsubstituted())
    for o in self.opcodes: self.output_opcode(outfd, o)

  def expand_opcode(self, o, prev):
    """Expand special opcodes into regular ones."""
    name, params = o
    l = []
    
    def append(name, *params):
      l.append((name, list(params)))
      for p in params: self.refers_to(p)

    def push_byte(object):
      if object.static_value() == 0:
        append('clrf', compiler['PREINC0'], access)
      else:
        append('movlw', object)
        append('movwf', compiler['PREINC0'], access)

    if name == 'OP_2>1':
      append('movff', params[0], compiler['PREINC0'])
      append('movff', params[1], compiler['PREINC0'])
    elif name == 'OP_PUSH_W':
      append('movwf', compiler['PREINC0'], access)
      append('clrf', compiler['PREINC0'], access)
    elif name == 'OP_POP_W':
      append('movf', compiler['POSTDEC0'], dst_w, access)
      append('movf', compiler['POSTDEC0'], dst_w, access)
    elif name == 'OP_PUSH':
      push_byte(low(params[0]))
      push_byte(high(params[0]))
    elif name == 'OP_FETCH':
      if ram_addr(params[0]):
        append('movff', params[0], compiler['PREINC0'])
        append('movff', Add(params[0], Number(1)),
                compiler['PREINC0'])
      else:
        push_byte(low(params[0]))
        push_byte(high(params[0]))
        append('call', compiler['op_fetch_tos'], no_fast)
    elif name == 'OP_CFETCH':
      if ram_addr(params[0]):
        append('movff', params[0], compiler['PREINC0'])
        append('clrf', compiler['PREINC0'], access)
      else:
        push_byte(low(params[0]))
        push_byte(high(params[0]))
        append('call', compiler['op_cfetch_tos'], no_fast)        
    elif name == 'OP_FETCH_TOS':
      append('call', compiler['op_fetch_tos'], no_fast)
    elif name == 'OP_CFETCH_TOS':
      append('call', compiler['op_cfetch_tos'], no_fast)
    elif name == 'OP_0=':      
      if prev and prev[0] == 'MARKER_ZSET':
        append('call', compiler['op_zeroeq_z'], no_fast)
      else:
        append('call', compiler['op_zeroeq'], no_fast)
    elif name == 'OP_NORMALIZE':
      if prev and prev[0] == 'MARKER_ZSET':
        append('call', compiler['op_normalize_z'], no_fast)
      else:
        append('call', compiler['op_normalize'], no_fast)
    elif name == 'OP_BIT_SET?':
      # The address is short-addressable
      append('movlw', Number(-1))
      append('btfss', params[0], params[1], params[2])
      append('addlw', Number(1))
      append('movwf', compiler['PREINC0'], access)
      append('movwf', compiler['PREINC0'], access)
    elif name == 'OP_BIT_CLR?':
      # The address is short-addressable
      append('movlw', Number(-1))
      append('btfsc', params[0], params[1], params[2])
      append('addlw', Number(1))
      append('movwf', compiler['PREINC0'], access)
      append('movwf', compiler['PREINC0'], access)
    elif name == 'OP_DUP':
      append('call', compiler['op_dup'], no_fast)
    elif name == 'OP_INTR_PROTECT':
      if compiler.use_interrupts:
        append('btfsc', compiler['INTCON'], Number(7), access)
        append('bsf', compiler['temp_gie'], Number(0), access)
        append('bcf', compiler['INTCON'], Number(7), access)
      else:
        append('EMPTY')
    elif name == 'OP_INTR_UNPROTECT':
      if compiler.use_interrupts:
        append('btfsc', compiler['temp_gie'], Number(0), access)
        append('bsf', compiler['INTCON'], Number(7), access)
        append('bcf', compiler['temp_gie'], Number(0), access)
      else:
        append('EMPTY')
      
    # Return the new version if it has changed, the original otherwise
    if l:
      if l == [('EMPTY', [])]: return []
      else: return l
    else: return [o]

  def expand(self):
    new_opcodes = []
    prev = None
    for o in self.opcodes:
      new_opcodes += self.expand_opcode(o, prev)
      if new_opcodes: prev = new_opcodes[-1]
    self.opcodes = new_opcodes

  def output_opcode(self, outfd, o):
    name, params = o
    def write_insn(insn): outfd.write('\t%s\n' % insn)
    if is_internal_jump(o): name = 'bra'
    if name in ['call', 'return'] and params[-1] == no_fast:
      params = params[:-1]
    if name == 'LABEL':
      outfd.write('%s\n' % params[0])
    elif name == 'COMMENT':
      outfd.write('; %s\n' % params[0])
    elif len(params) == 0:
      write_insn(name)
    elif len(params) == 1:
      write_insn('%s %s' %(name, params[0]))
    elif len(params) == 2:
      write_insn('%s %s,%s' %(name, params[0], params[1]))
    elif len(params) == 3:
      write_insn('%s %s,%s,%s' %(name, params[0], params[1], params[2]))
    else:
      raise Compiler.UNIMPLEMENTED, (name, params)

  def static_value(self):
    return None

class Input:

  def __init__(self, name, lines):
    self.name = name
    self.lines = lines
    self.current_line = 0

  def next_line(self):
    self.current_line += 1
    if self.current_line > len(self.lines): return None
    else: return self.lines [self.current_line - 1]

  def current_location(self):
    """Return an identifier of the current location"""
    return "%s:%d" %(self.name, self.current_line)

  def __repr__(self):
    return self.current_location()

class Compiler:

  EOF = 'eof'
  FATAL_ERROR = 'fatal_error'
  UNIMPLEMENTED = 'unimplemented'
  INTERNAL_ERROR = 'internal_error'
  COMPILATION_ERROR = 'compilation_error'

  pic_opcodes = ['clrwdt', 'daw', 'nop', 'sleep', 'reset',
                'tblrd*', 'tblrd*+', 'tblrd*-', 'tblrd+*',
                 'tblwt*', 'tblwt*+', 'tblwt*-', 'tblwt+*']
  
  pic_opcodes_l = ['bc', 'bn', 'bnc', 'bnn', 'bnov', 'bnz', 'bov',
                   'bra', 'bz', 'goto', 'rcall',
                   'addlw', 'andlw', 'iorlw', 'movlb',
                   'movlw', 'mullw', 'retlw', 'sublw', 'xorlw']

  pic_opcodes_s = ['return', 'retfie']
                   
  pic_opcodes_la = ['clrf', 'cpfseq', 'cpfsgt', 'cpfslt',
                    'movwf', 'mulwf', 'negf', 'setf', 'tstfsz',
                    'lfsr']

  pic_opcodes_ll = ['movff']

  pic_opcodes_ls = ['call']

  pic_opcodes_lfa = ['addwf', 'addwfc', 'andwf', 'comf', 'decf',
                     'decfsz', 'dcfsnz', 'incf', 'incfsz', 'infsnz',
                     'iorwf', 'movf', 'rlcf', 'rlncf', 'rrcf', 'rrncf',
                     'subfwb', 'subwf', 'subwfb', 'swapf',
                     'xorwf']

  pic_opcodes_lla = ['bcf', 'bsf', 'btfsc', 'btfss', 'btg']

  all_opcodes = pic_opcodes + pic_opcodes_l + pic_opcodes_s + \
                pic_opcodes_la + pic_opcodes_ll + pic_opcodes_ls + \
                pic_opcodes_lfa

  gpasm_directives = ['__badram', '__config', '__idlocs', '__maxram',
                      'bankisel', 'banksel', 'cblock', 'code', 'constant',
                      'da', 'data', 'db', 'de', 'dt', 'dw', 'else',
                      'end', 'endc', 'endif', 'endm', 'endw', 'equ',
                      'error', 'errorlevel', 'extern', 'exitm',
                      'expand', 'fill', 'global', 'high',
                      'idata', 'if', 'ifdef',
                      'ifndef', 'list', 'local', 'low', 'macro', 'messg',
                      'noexpand', 'nolist', 'org', 'page', 'pagesel',
                      'processor', 'radix', 'res', 'set', 'space',
                      'subtitle', 'title', 'udata', 'udata_acs',
                      'udata_ovr', 'udata_shr', 'variable', 'while']
                      
  def __init__(self, processor, start, main, automatic_inlining,
               no_comments, infile, asmfile):
    self.processor = processor
    self.start = start
    self.main = main
    self.automatic_inlining = automatic_inlining
    self.no_comments = no_comments
    self.infile = infile
    self.asmfile = asmfile
    self.dict = {}                  # Words as currently seen
    self.first_dict = {}            # Words as they were first defined
    self.input = None
    self.input_stack = []
    self.input_buffer = ''
    self.state = 0
    self.data_stack = []
    self.loaded_files = []
    self.all_entities = []
    self.object_stack = []
    self.here = 0x0
    self.eehere = 0x1000
    self.initialize_variables = False
    self.order = 0
    self.use_interrupts = False
    self.inline_list = []
    self.low_interrupt = None
    self.high_interrupt = None

  def process(self):
    self.add_default_content()
    self.include(self.infile)
    self.output(open(self.asmfile, 'w'))

  def enable_interrupts(self):
    if self.first_dict:
      self.error("interrupts need to be enabled at the beginning")
    self.use_interrupts = True

  def check_interrupts(self):
    if not compiler.use_interrupts:
      raise Compiler.FATAL_ERROR, \
            "%s: interrupts need to be enabled with -i" % \
            self.current_location()
    
  def add_default_content(self):
    self.add_asm_instructions()
    self.add_primitives()
    assert(self.here < 0x60)
    self.here = 0x100
    self.initialize_variables = True

  def add_primitives(self):
    self.add_primitive(':', Colon)
    self.add_primitive(';', SemiColon)
    self.add_primitive('exit', Exit)
    self.add_primitive('constant', MakeConstant)
    self.add_primitive('create', Create)
    self.add_primitive('allot', Allot)
    self.add_primitive('variable', MakeVariable)
    self.add_primitive('cvariable', MakeCVariable)
    self.add_primitive('eevariable', MakeEEVariable)
    self.add_primitive('eecvariable', MakeEECVariable)
    self.add_primitive('value', MakeValue)
    self.add_primitive('to', To)
    self.add_primitive('bit', MakeBit)
    self.add_primitive('recurse', Recurse)
    self.add_primitive('\\', Backslash)
    self.add_primitive('(', Parenthesis)
    self.add_primitive('@', Fetch)
    self.add_primitive('!', Store)
    self.add_primitive('c@', CFetch)
    self.add_primitive('c!', CStore)
    self.add_primitive('code', Code)
    self.add_primitive(';code', SemiColonCode)
    self.add_primitive('python', Python)
    self.add_primitive('label', MakeLabel)
    self.add_primitive('forward', MakeForward)
    self.add_primitive(',a', CommaA)
    self.add_primitive(',0', Comma0)
    self.add_primitive(',1', Comma1)
    self.add_primitive(',w', CommaW)
    self.add_primitive(',f', CommaF)
    self.add_primitive(',s', CommaS)
    self.add_primitive(',', Comma)
    self.add_primitive('c,', CComma)
    self.add_primitive('prefix', Prefix)
    self.add_primitive('postfix', Postfix)
    self.add_primitive('include', Include)
    self.add_primitive('needs', Needs)
    self.add_primitive('char', Char)
    self.add_primitive('[char]', Char)
    self.add_primitive('begin', Begin)
    self.add_primitive('again', Again)
    self.add_primitive('while', While)
    self.add_primitive('repeat', Repeat)
    self.add_primitive('ahead', Ahead)
    self.add_primitive('until', Until)
    self.add_primitive('cfor', CFor)
    self.add_primitive('cnext', CNext)
    self.add_primitive('if', If)
    self.add_primitive('then', Then)
    self.add_primitive('else', Else)
    self.add_primitive('=', Equal)
    self.add_primitive('0=', ZeroEqual)
    self.add_primitive('0<>', Normalize)
    self.add_primitive('bit-set?', BitSetTest)
    self.add_primitive('bit-clr?', BitClrTest)
    self.add_primitive('bit-mask', BitMask)
    self.add_primitive('bit-set', BitSet)
    self.add_primitive('bit-clr', BitClr)
    self.add_primitive('bit-toggle', BitToggle)
    self.add_primitive('inline', Inline)
    self.add_primitive('no-inline', NoInline)
    self.add_primitive('low-interrupt', LowInterrupt)
    self.add_primitive('high-interrupt', HighInterrupt)
    self.add_primitive('fast', Fast)
    self.add_primitive('>w', ToW)
    self.add_primitive('w>', FromW)
    self.add_primitive('drop', Drop)
    self.add_primitive('dup', Dup)
    self.add_primitive('inw', InW)    
    self.add_primitive('outw', OutW)
    self.add_primitive('outz', OutZ)
    self.add_primitive('2>1', TwoToOne)
    self.add_primitive('+', Plus)
    self.add_primitive('-', Minus)
    self.add_primitive('1+', OnePlus)
    self.add_primitive('1+!', OnePlusStore)
    self.add_primitive('1-', OneMinus)
    self.add_primitive('c+!', CPlusStore)
    self.add_primitive('c-!', CMinusStore)
    self.add_primitive('lshift', LShift)
    self.add_primitive('literal', Literal)
    self.add_primitive('[', OpeningBracket)
    self.add_primitive(']', ClosingBracket)
    self.add_primitive('intr-protect', IntrProtect)
    self.add_primitive('intr-unprotect', IntrUnprotect)
    self.add_primitive('switchw', SwitchW)
    self.add_primitive('casew', CaseW)
    self.add_primitive('endswitchw', EndSwitchW)
    self.add_primitive('and', LAnd)
    self.add_primitive("[']", AddressOf)
    self.add_primitive("jump", Jump)
    self.include('lib/core.fs')
    if self.use_interrupts:
      self.include('lib/interrupts.fs')

  def push_object(self, object):
    """Temporarily install object as the current object."""
    self.object_stack.append((self.current_object, self.state))
    self.current_object = object
    self.state = 1

  def pop_object(self):
    """Restore previously saved object from the object stack."""
    self.current_object, self.state = self.object_stack[-1]
    del self.object_stack[-1]

  def push_init_runtime(self):
    """Temporarily install init_runtime as the current object."""
    self.push_object(self['init_runtime'])

  def current_location(self):
    """Return an identifier of the current location"""
    if self.input: return self.input.current_location()
    else: return '<builtin>'

  def allot(self, n):
    self.here += n

  def save_input(self):
    self.input_stack.append(self.input)

  def restore_input(self):
    self.input, self.input_stack = self.input_stack[-1], self.input_stack[:-1]

  def include(self, filename):
    self.loaded_files.append(filename)
    self.save_input()
    self.run(Input(filename, open(filename, 'r').readlines()))
    self.restore_input()

  def needs(self, filename):
    if filename not in self.loaded_files: self.include(filename)

  def interpret(self, str):
    self.save_input()
    self.run(Input('<interpreter>', [str]))
    self.restore_input()

  def refill(self):
    next_line = self.input.next_line()
    if next_line is None:
      raise Compiler.EOF, "%s: end of file" % self.current_location()
    if not next_line: return self.refill()
    self.input_buffer = next_line

  def next_char(self):
    if not self.input_buffer: return None
    char, self.input_buffer = self.input_buffer[0], self.input_buffer[1:]
    return char

  def parse(self, char):
    result, self.input_buffer = self.input_buffer.split(char, 1)
    return result

  _next_word = sre.compile('(\s*)(\S+)\s?')

  def parse_word(self):
    while True:
      x = Compiler._next_word.match(self.input_buffer)
      if x:
        word = x.group(2)
        self.input_buffer = self.input_buffer[len(word)+len(x.group(1))+1:]
        return word
      self.refill()

  def add_primitive(self, name, object_class):
    object_class(name)
    self.enter()

  def add_asm_instruction(self, name, format):
    PICIns(name, format)
    self.enter()

  def add_pic_instructions(self, list, format):
    for i in list: self.add_asm_instruction(i, format)

  def add_asm_instructions(self):
    self.add_pic_instructions(Compiler.pic_opcodes, '')
    self.add_pic_instructions(Compiler.pic_opcodes_l, 'l')
    self.add_pic_instructions(Compiler.pic_opcodes_s, 's')
    self.add_pic_instructions(Compiler.pic_opcodes_la, 'la')
    self.add_pic_instructions(Compiler.pic_opcodes_ll, 'll')
    self.add_pic_instructions(Compiler.pic_opcodes_ls, 'ls')
    self.add_pic_instructions(Compiler.pic_opcodes_lfa, 'lfa')
    self.add_pic_instructions(Compiler.pic_opcodes_lla, 'lla')

  def start_compilation(self, object):
    self.current_object = object

  def find(self, name):
    try: return self.dict[string.lower(name)]
    except: return None

  def enter_object(self, object):
    object.order = self.order
    self.order += 1
    while True:
      previous = self.find(object.name)
      if previous is None: break
      if not isinstance(previous, Forward):
        if previous.from_source:
          self.warning('redefinition of %s(defined at %s)' %
                       (previous.name, previous.definition))
        break
      self.fix_forward(previous, object)
      self.mask(previous)
    self.all_entities.append(object)
    if previous: occurrence = previous.occurrence + 1
    else: occurrence = 0
    self.dict[string.lower(object.name)] = object
    if occurrence == 0: self.first_dict[string.lower(object.name)] = object
    object.occurrence = occurrence
    if `object` in self.inline_list: object.inlined = True

  def fix_forward(self, old, new):
    self.all_entities.remove(old)
    new.occurrence = old.occurrence
    def fix_it(o):
      if o == old: return new
      else: return o
    for e in [self.current_object] + self.all_entities:
      if not e.immediate:
        e.opcodes = [(name, [fix_it(p) for p in params])
                     for(name, params) in e.opcodes]
        e.references = [fix_it(r) for r in e.references]

  def mask(self, object):
    """Mask a given object by its previous occurrence if it exists."""
    del self.dict[object.name]
    for e in self.all_entities:
      if e != object and e.name == object.name:
        self.dict[e.name] = e

  def enter(self):
    self.enter_object(self.current_object)
    
  def ct_push(self, value):
    self.data_stack = [value] + self.data_stack

  def ct_pop(self):
    value, self.data_stack = self.data_stack [0], self.data_stack [1:]
    return value

  def ct_swap(self):
    l1 = self.ct_pop()
    l2 = self.ct_pop()
    self.ct_push(l1)
    self.ct_push(l2)

  def run(self, input):
    self.input = input
    self.input_buffer = ''
    while True:
      try: word = self.parse_word()
      except Compiler.EOF: return
      object = self.find(word)
      if object:
        if object.immediate:
          try: object.run()
          except:
            self.error('internal error')
            raise
        else:
          if self.state: self.add_call(object)
          else: self.ct_push(object)
      else:
        number = parse_number(word)
        if number is None:
          input = self.input
	  raise Compiler.FATAL_ERROR, \
                "%s: unknown word %s" %(self.current_location(),
                                         word)
        if self.state:
		self.push(number)
        else:
		self.ct_push(number)

  def output(self, outfd):
    global compiler
    # Finalize init_runtime
    self.current_object = self['init_runtime']
    self.state = 1
    inlinable = [x for x in self.all_entities if x.can_inline()]
    refs = self.find(self.main).deep_references([])
    if self.automatic_inlining:
      to_inline = [x for x in refs if x in inlinable and x.should_inline()]
      if to_inline:
        stderror("Restarting with automatic inlining of:\n   %s" %
                  "\n   ".join(["%s(%s)" %(x.name, x.definition)
                                 for x in to_inline]))
        outfd.close()
        compiler = Compiler(self.processor, self.start, self.main,
                             self.automatic_inlining, self.no_comments,
                            self.infile, self.asmfile)
        compiler.inline_list = self.inline_list + [`x` for x in to_inline]
        if self.use_interrupts: compiler.enable_interrupts()
        compiler.process()
        return
    if compiler['FSR0H'] in refs or compiler['FSR0L'] in refs \
       or compiler['POSTINC0'] in refs or compiler['POSTDEC0'] in refs \
       or compiler['PREINC0'] in refs:
      compiler.add_call(compiler['init_stack'])
    if compiler['FSR2H'] in refs or compiler['FSR2L'] in refs \
       or compiler['POSTINC2'] in refs or compiler['POSTDEC2'] in refs \
       or compiler['PREINC2'] in refs:
      compiler.add_call(compiler['init_rstack'])
    self.add_instruction('goto', [self.find(self.main)])
    self.current_object.refers_to(self.find(self.main))
    self.state = 0
    root = self.current_object
    # Force expansion of main and friends to make potential renaming
    # of main possible
    root.deep_references([])
    roots = [root]
    if self.low_interrupt:
      self.low_interrupt.deep_references([])
      roots.append(self.low_interrupt)
    if self.high_interrupt:
      self.high_interrupt.deep_references([])
      roots.append(self.high_interrupt)
    self.output_prologue(outfd)
    self.deep_output(outfd, roots)
    self.output_epilogue(outfd)

  def count_references(self, l):
    """Count references to each word within list l."""
    for i in l:
      for o in range(len(i.opcodes)):
        if i.opcodes[o][1]:
          r = i.opcodes[o][1][0]
          if r in l and o+1 == len(i.opcodes):
              i.nrefs += 50
              r.nrefs += 100

  def reorder(self, l):
    """Find a good order for outputting the code section in which fallbacks
    through other words are used when possible. We favour highly used words
    as they are likely to be called more often."""
    self.count_references(l)
    l.sort(lambda x, y: cmp(y.nrefs, x.nrefs))
    r = []
    for i in l:
      if i.substitute: continue
      # If a word already in the list ends with a goto to this word,
      # insert it afterwards and remove the final goto. The previous word
      # will then fallback through the new one.
      for j in range(len(r)):
        name, params = r[j].opcodes[-1]
        if last_goto(r[j]) and isinstance(params[0], Word) and \
               params[0].real_instance() == i:
          del r[j].opcodes[-1]
          r = r[:j+1] + [i] + r[j+1:]
          break
      else:
        # If this words ends with a goto to a word already in r and the
        # previous word is not a fallback, insert it before and remove the
        # final instruction to get a fallback.
        name, params = i.opcodes[-1]
        if name == 'goto' and params[0] in r:
          n = r.index(params[0])
          if n == 0 or last_goto(r[n-1]):
            del i.opcodes[-1]
            r = r[:n] + [i] + r[n:]
            continue
        r.append(i)
    return r

  def deep_output(self, outfd, roots):
    l = []
    for r in roots:
      p = [x for x in r.deep_references([]) if not isinstance(x, Label)]
      for i in p:
        if i not in l: l.append(i)
    l.sort(lambda x, y: cmp(x.order, y.order))
    sections = []
    for i in l:
      if i.section not in sections: sections.append(i.section)
    for s in sections:
      self.output_section_header(outfd, s)
      g = l
      if s == 'code': g = self.reorder([x for x in l if x.section == 'code'])
      for i in g:
        if i.section == s:
          outfd.write('\n')
          if not compiler.no_comments: i.output_header(outfd)
          i.output(outfd)
    outfd.write('\n')

  def output_section_header(self, outfd, name):
    t = '---------------------------------------------------------'
    outfd.write('\n;%s\n; Section: %s\n;%s\n' %(t, name, t))

  def output_prologue(self, outfd):
    outfd.write("\tprocessor pic%s\n" % self.processor)
    outfd.write("\tradix dec\n")
    outfd.write("\torg %s\n" % self.start)
    outfd.write("\tgoto %s\n" % self['init_runtime'])
    if self.high_interrupt:
      outfd.write("\torg %s\n" %(self.start + 8))
      outfd.write("\tgoto %s\n" % self.high_interrupt)
    if self.low_interrupt:
      outfd.write("\torg %s\n" %(self.start + 0x18))
      outfd.write("\tgoto %s\n" % self.low_interrupt)

  def output_epilogue(self, outfd):
    outfd.write("END\n")

  def warning(self, str):
    warning('%s: %s' %(self.current_location(), str))

  def error(self, str):
    error('%s: %s' %(self.current_location(), str))

  def add_instruction(self, instruction, params = []):
    self.current_object.add_instruction(instruction, params)

  def add_call(self, target):
    if target.inw: self['>w'].run()
    if target.inlined: self.inline_call(target)        
    else: self.add_instruction('call', [target, no_fast])
    if target.outw: self['w>'].run()
    if target.outz:
      self.add_instruction('MARKER_ZSET', [])
      self.add_instruction('OP_NORMALIZE', [])

  def inline_call(self, target):
    # Collect labels
    labels = [o[1][0] for o in target.opcodes if o[0] == 'LABEL']
    # Build replacement map
    rep = {}
    for l in labels: rep[l] = Label()
    # Replace label in every opcode(they are alone as parameters)
    # as we inline them. The final return must not be inlined.
    # Also, warn if external goto or return are detected; we do not perform
    # this check in inline assembly code.
    removable_end = True
    for n, p in target.opcodes[:-1]:
      if is_internal_jump((n, p)) and p == target.end_label:
        removable_end = False
      if is_external_jump((n, p)):
        self.warning('inlining of %s uses a non-local jump' % target.name)
      if p and p[0] in rep: self.add_instruction(n, [rep[p[0]]])
      else: self.add_instruction(n, p)
    # If there were no multiple exits, remove the end_label so that
    # optimizations can be performed between the inlined word and
    # subsequent instructions.
    if removable_end:
      # Check that the latest opcode was a return or an inlined call to return.
      assert(target.opcodes[-1][0] == 'return')
      name, params = compiler.last_instruction()
      if name == 'LABEL' and params == [rep[target.end_label]]:
        compiler.rewind()
    # Transfer dependencies from target to current object
    for r in target.references: self.current_object.refers_to(r)

  def tos_to_addr_byte(self, addr):
    self.pop_w()
    self.add_instruction('movff', [self['POSTDEC0'], addr])

  def tos_to_addr(self, addr):
    self.add_instruction('movff', [self['POSTDEC0'],
                                    Add(addr, Number(1))])
    # Using movff with PCL as a target is forbidden. This route will
    # always be taken even if a constant has been pushed onto the stack
    # because PCLATU has been cleared in the meantime.
    if addr.static_value() != self['PCL'].static_value():
      self.add_instruction('movff', [self['POSTDEC0'], addr])
    else:
      self.add_instruction('movf', [self['POSTDEC0'], dst_w, access])
      self.add_instruction('movwf', [addr, access])

  def push_byte(self, object):
    if object.static_value() == 0:
      self.add_instruction('clrf', [self['PREINC0'], access])
    else:
      self.add_instruction('movlw', [object])
      self.push_w()

  def push_w(self):
    self.add_instruction('movwf', [self['PREINC0'], access])

  def push(self, object):
    self.add_instruction('OP_PUSH', [object])

  def pop_w(self):
    self.add_instruction('movf', [self['POSTDEC0'], dst_w, access])
    
  def pop_to_fsr(self, fsr):
    if is_static_push(self.last_instruction()):
      name, params = self.last_instruction()
      self.rewind()
      self.add_instruction('lfsr', [Number(fsr), params[0]])
    else:
      self.add_instruction('movff', [self['POSTDEC0'],
                                      self['FSR%dH' % fsr]])
      self.add_instruction('movff', [self['POSTDEC0'],
                                      self['FSR%dL' % fsr]])
    
  def last_instruction(self):
    try: return self.current_object.opcodes[-1]
    except: return None, []

  def before_last_instruction(self):
    try: return self.current_object.opcodes[-2]
    except: return None, []
    
  def rewind(self):
    self.current_object.opcodes = self.current_object.opcodes[:-1]

  def __getitem__(self, item):
    """Return the first defined entity with name given in item. If the
    value is a bit definition, return a list with both address and bit
    number."""
    e = self.first_dict[string.lower(item)]
    if isinstance(e, Bit):
      return [e.value, e.bit]
    elif e is not None:
      return e
    else:
      raise Compiler.INTERNAL_ERROR, \
            "%s: cannot find internal entity %s" %(self.current_location(),
                                                    item)

def set_start_cb(option, opt, value, parser):
  s = parse_number(value)
  if s is None:
    raise optparse.OptionValueError, "%s is not a valid address" % value
  setattr(parser.values, 'start', s)

def main():
  global compiler
  parser = optparse.OptionParser(usage = '%prog [options] FILE')
  parser.add_option('-a', '--auto-inline', action = 'store_true',
                     default = False, dest = 'automatic_inlining',
                     help = 'turn on automatic inlining')
  parser.add_option('-c', '--compile', action = 'store_true',
                     default = False, dest = 'compile_only',
                     help = 'compile only, do not link')
  parser.add_option('-i', '--interrupts', dest = 'enable_interrupts',
                     action = 'store_true', default = False,
                     help = 'enable interrupts usage')
  parser.add_option('-m', '--main', dest = 'root', metavar = 'WORD',
                     default = 'main',
                     help = 'main word [main]')
  parser.add_option('-N', '--no-comments', dest = 'no_comments',
                    default = False, action = 'store_true')
  parser.add_option('-o', '--output', metavar = 'FILE', dest = 'outfile',
                     help = 'set output file name', default = None)
  parser.add_option('-p', '--processor', metavar = 'MODEL',
                     default = '18f248',
                     help = 'set processor type [18f248]')
  parser.add_option('-s', '--start', default = parse_number('0x2000'),
                     action = 'callback', callback = set_start_cb,
                     metavar = 'ADDR', type='string',
                     help = 'set starting address [0x2000]')
  opts, args = parser.parse_args()
  if len(args) != 1:
    parser.print_help()
    sys.exit(1)
  infile = args[0]
  # Do the real job
  asmfile = os.path.splitext(infile)[0] + '.asm'
  hexfile = os.path.splitext(infile)[0] + '.hex'
  if opts.outfile:
    if opts.compile_only: asmfile = opts.outfile
    else: hexfile = opts.outfile
  compiler = Compiler(opts.processor, opts.start, opts.root,
                       opts.automatic_inlining, opts.no_comments,
                       infile, asmfile)
  if opts.enable_interrupts: compiler.enable_interrupts()
  compiler.process()
  if not opts.compile_only:
    if os.fork() == 0:
      os.execlp('gpasm', 'gpasm', '-o', hexfile, asmfile)
    else:
      pid, status = os.wait()
      if status != 0: sys.exit(1)

if __name__ == '__main__':
  try: import psyco
  except: pass
  main()
