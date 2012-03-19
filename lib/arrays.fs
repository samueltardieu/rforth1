python

def parse_array():
    data = []
    while True:
        word = compiler.parse_word()
        if word == '}':
            break
        number = parse_number(word)
        if number is None:
            raise Compiler.FATAL_ERROR("%s: unrecognized number %s" % (compiler.current_location(), word))
        data.append(number.value)
    return data

def insert_data(data, kind):
    data = [d & 0xff for d in data]
    ref = FlashData(data, "a %s array" % kind, compiler.current_object.name + "_array")
    ref.from_source = False
    compiler.push(ref)

class Array (Primitive):

    def run(self):
        d = []
        for i in parse_array():
            d.append(i)
            d.append(i >> 8)
        insert_data(d, "words")

class CArray (Primitive):

    def run(self):
        insert_data(parse_array(), "bytes")

compiler.add_primitive ('{', Array)
compiler.add_primitive('c{', CArray)

;python
