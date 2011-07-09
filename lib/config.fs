python
class Config(Primitive):

  class ConfigItem(Primitive):
    section = 'configuration'
    def output(self, outfd):
      outfd.write('\tCONFIG %s=%s\n' % self.item)

  def run(self):
    option, value = compiler.parse_word(), compiler.parse_word()
    item = self.ConfigItem(option)
    item.item = (option, value)
    compiler.enter_object(item)
    # These next 3 lines ensure the config items are attached to
    # the parse tree, so that they are always emitted in the assembler
    compiler.push_init_runtime()
    compiler.current_object.refers_to(item)
    compiler.pop_object()

compiler.add_primitive('config', Config)
;python
